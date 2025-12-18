using System.Text.Json;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ScheduledMessageProcessorTests : DbTestsBase
{
    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ScheduledMessageProcessor_Produces_ConsumerMessages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);

            var consumerMessage = new MultiConsumerMessage
            {
                Name = "test-name"
            };
            var serializedMessage = JsonSerializer.Serialize(consumerMessage);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var scheduledMessageService =
                    scope.ServiceProvider.GetRequiredService<IScheduleService>();

                scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC", "test-tag");

                await dbContext.SaveChangesAsync();
            }

            // When
            FakeTime.SetUtcNow(FakeTime.GetUtcNow().AddSeconds(1));
            var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.ShouldBe(1);
            using (var scope = serviceProvider.CreateScope())
            {
                var postDbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.ShouldBe(2);

                var message1 =
                    await postDbContext.AssertSingleConsumerMessageById<MultiConsumer1, MultiConsumerMessage>(
                        consumerMessage, "fake-id-2");
                message1.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().ToUnixTimeSeconds());
                message1.Attempts.ShouldBe(0);
                message1.InsertId.ShouldBe("fake-id-1");
                message1.Id.ShouldBe("fake-id-2");
                message1.ConsumerType = nameof(MultiConsumer1);
                message1.PayloadType = nameof(MultiConsumerMessage);
                message1.Payload.ShouldBe(serializedMessage);

                var message2 =
                    await postDbContext.AssertSingleConsumerMessageById<MultiConsumer2, MultiConsumerMessage>(
                        consumerMessage, "fake-id-3");
                message2.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().ToUnixTimeSeconds());
                message2.Attempts.ShouldBe(0);
                message2.InsertId.ShouldBe("fake-id-1");
                message2.Id.ShouldBe("fake-id-3");
                message2.ConsumerType = nameof(MultiConsumer2);
                message2.PayloadType = nameof(MultiConsumerMessage);
                message2.Payload.ShouldBe(serializedMessage);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task ScheduledMessageProcessor_Increments_Available_After(TestDbContainerBase dbContainer)
    {
        // Given
        try
        {
            var serviceProvider = await Setup(dbContainer);

            var consumerMessage = new MultiConsumerMessage
            {
                Name = "test-name"
            };

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var scheduledMessageService =
                    scope.ServiceProvider.GetRequiredService<IScheduleService>();

                scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC", "test-tag");

                await dbContext.SaveChangesAsync();
            }

            FakeTime.SetUtcNow(FakeTime.GetUtcNow().AddSeconds(100));
            // When
            var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.ShouldBe(1);
            using (var scope = serviceProvider.CreateScope())
            {
                var postDbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message = await postDbContext.AssertSingleScheduledMessage(consumerMessage);
                message!.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().AddSeconds(1).ToUnixTimeSeconds());
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}