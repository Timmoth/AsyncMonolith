using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

public class ConsumerMessageProcessorTests : DbTestsBase
{
    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Invokes_Consumer(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.ShouldBe(1);
            TestConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Returns_0_If_No_Available_Messages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                }, FakeTime.GetUtcNow().ToUnixTimeSeconds() + 100);

                await dbContext.SaveChangesAsync();
            }

            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            // When
            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.ShouldBe(0);
            TestConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(0);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Increments_Message_Attempts(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            var consumerMessage = new ExceptionConsumerMessage
            {
                Name = "test-name"
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(consumerMessage);

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ProcessBatch(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message =
                    await dbContext.AssertSingleConsumerMessage<ExceptionConsumer, ExceptionConsumerMessage>(
                        consumerMessage);
                message?.Attempts.ShouldBe(1);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Increments_Message_Attempts_OnTimeout(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var consumerMessage = new TimeoutConsumerMessage
            {
                Delay = 2
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(consumerMessage);

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ProcessBatch(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message =
                    await dbContext.AssertSingleConsumerMessage<TimeoutConsumer, TimeoutConsumerMessage>(
                        consumerMessage);
                message?.Attempts.ShouldBe(1);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Increments_Message_AvailableAfter(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var consumerMessage = new ExceptionConsumerMessage
            {
                Name = "test-name"
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(consumerMessage);

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ProcessBatch(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message =
                    await dbContext.AssertSingleConsumerMessage<ExceptionConsumer, ExceptionConsumerMessage>(
                        consumerMessage);
                message?.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().ToUnixTimeSeconds() + 10);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Moves_Message_To_PoisonedMessages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            ConsumerMessage message;
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(new ExceptionConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
                message = await dbContext.ConsumerMessages.SingleAsync();
                message.Attempts = 10;
                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ProcessBatch(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var consumerMessageExists = await dbContext.ConsumerMessages.AnyAsync();
                consumerMessageExists.ShouldBeFalse();
                var poisonedMessage = await dbContext.PoisonedMessages.SingleAsync();
                poisonedMessage.Id.ShouldBe(message.Id);
                poisonedMessage.AvailableAfter.ShouldBe(message.AvailableAfter);
                poisonedMessage.ConsumerType.ShouldBe(message.ConsumerType);
                poisonedMessage.CreatedAt.ShouldBe(message.CreatedAt);
                poisonedMessage.Payload.ShouldBe(message.Payload);
                poisonedMessage.PayloadType.ShouldBe(message.PayloadType);
                poisonedMessage.Attempts.ShouldBe(message.Attempts + 1);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Moves_Message_To_PoisonedMessages_OnCustomMaxAttempts(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            ConsumerMessage message;
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(new ExceptionConsumer2AttemptsMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
                message = await dbContext.ConsumerMessages.SingleAsync();
                message.Attempts = 1;
                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ProcessBatch(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var consumerMessageExists = await dbContext.ConsumerMessages.AnyAsync();
                consumerMessageExists.ShouldBeFalse();
                var poisonedMessage = await dbContext.PoisonedMessages.SingleAsync();
                poisonedMessage.Id.ShouldBe(message.Id);
                poisonedMessage.AvailableAfter.ShouldBe(message.AvailableAfter);
                poisonedMessage.ConsumerType.ShouldBe(message.ConsumerType);
                poisonedMessage.CreatedAt.ShouldBe(message.CreatedAt);
                poisonedMessage.Payload.ShouldBe(message.Payload);
                poisonedMessage.PayloadType.ShouldBe(message.PayloadType);
                poisonedMessage.Attempts.ShouldBe(message.Attempts + 1);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Removes_Consumed_Messages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();

                await producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.ShouldBe(1);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var remaining = await dbContext.ConsumerMessages.CountAsync();
                remaining.ShouldBe(0);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}