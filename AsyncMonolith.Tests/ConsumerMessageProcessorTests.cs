using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

            // Then
            consumedMessage.Should().BeTrue();
            TestConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).Should().Be(1);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ConsumerMessageProcessor_Returns_False_If_No_Available_Messages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                }, FakeTime.GetUtcNow().ToUnixTimeSeconds() + 100);

                await dbContext.SaveChangesAsync();
            }

            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            // When
            var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

            // Then
            consumedMessage.Should().BeFalse();
            TestConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).Should().Be(0);
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

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new ExceptionConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ConsumeNext(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message = await dbContext.ConsumerMessages.FirstOrDefaultAsync();
                message?.Attempts.Should().Be(1);
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

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new ExceptionConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            await processor.ConsumeNext(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message = await dbContext.ConsumerMessages.FirstOrDefaultAsync();
                message?.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds() + 10);
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
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new ExceptionConsumerMessage
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

            await processor.ConsumeNext(CancellationToken.None);

            // Then
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var consumerMessageExists = await dbContext.ConsumerMessages.AnyAsync();
                consumerMessageExists.Should().BeFalse();
                var poisonedMessage = await dbContext.PoisonedMessages.SingleAsync();
                poisonedMessage.Id.Should().Be(message.Id);
                poisonedMessage.AvailableAfter.Should().Be(message.AvailableAfter);
                poisonedMessage.ConsumerType.Should().Be(message.ConsumerType);
                poisonedMessage.CreatedAt.Should().Be(message.CreatedAt);
                poisonedMessage.Payload.Should().Be(message.Payload);
                poisonedMessage.PayloadType.Should().Be(message.PayloadType);
                poisonedMessage.Attempts.Should().Be(message.Attempts + 1);
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

            var preMessageCount = 0;
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

                producer.Produce(new SingleConsumerMessage
                {
                    Name = "test-name"
                });

                await dbContext.SaveChangesAsync();

                preMessageCount = await dbContext.ConsumerMessages.CountAsync();
            }

            // When
            var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

            // Then
            consumedMessage.Should().BeTrue();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var remaining = await dbContext.ConsumerMessages.CountAsync();
                remaining.Should().Be(preMessageCount - 1);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}