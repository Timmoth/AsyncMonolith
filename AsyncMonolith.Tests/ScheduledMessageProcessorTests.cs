using System.Text.Json;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
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
                    scope.ServiceProvider.GetRequiredService<ScheduleService<TestDbContext>>();

                scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC", "test-tag");

                await dbContext.SaveChangesAsync();
            }

            // When
            FakeTime.SetUtcNow(FakeTime.GetUtcNow().AddSeconds(1));
            var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.Should().Be(1);
            using (var scope = serviceProvider.CreateScope())
            {
                var postDbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.Should().Be(2);

                var message1 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer1)));
                message1.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
                message1.Attempts.Should().Be(0);
                message1.InsertId.Should().Be("fake-id-1");
                message1.Id.Should().Be("fake-id-2");
                message1.ConsumerType = nameof(MultiConsumer1);
                message1.PayloadType = nameof(MultiConsumerMessage);
                message1.Payload.Should().Be(serializedMessage);

                var message2 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer2)));
                message2.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
                message2.Attempts.Should().Be(0);
                message2.InsertId.Should().Be("fake-id-1");
                message2.Id.Should().Be("fake-id-3");
                message2.ConsumerType = nameof(MultiConsumer2);
                message2.PayloadType = nameof(MultiConsumerMessage);
                message2.Payload.Should().Be(serializedMessage);
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
                    scope.ServiceProvider.GetRequiredService<ScheduleService<TestDbContext>>();

                scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC", "test-tag");

                await dbContext.SaveChangesAsync();
            }

            FakeTime.SetUtcNow(FakeTime.GetUtcNow().AddSeconds(100));
            // When
            var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

            var consumedMessage = await processor.ProcessBatch(CancellationToken.None);

            // Then
            consumedMessage.Should().Be(1);
            using (var scope = serviceProvider.CreateScope())
            {
                var postDbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var message = await postDbContext.ScheduledMessages.FirstOrDefaultAsync();
                message.Should().NotBeNull();
                message!.AvailableAfter.Should().Be(FakeTime.GetUtcNow().AddSeconds(1).ToUnixTimeSeconds());
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}