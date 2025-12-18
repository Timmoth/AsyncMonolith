using System.Text.Json;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ScheduledMessageServiceTests : DbTestsBase
{
    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task Schedule_Writes_Scheduled_Message(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };

            var scheduledMessageService = serviceProvider.GetRequiredService<IScheduleService>();
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var tag = "test-tag";

            // When
            scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC", tag);
            await dbContext.SaveChangesAsync();

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var message = await postDbContext.AssertSingleScheduledMessage(consumerMessage);
                message.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().ToUnixTimeSeconds() + 1);
                message.Id.ShouldBe("fake-id-0");
                message.Tag.ShouldBeEquivalentTo(tag);
                message.PayloadType = nameof(SingleConsumerMessage);
                message.Payload.ShouldBe(JsonSerializer.Serialize(consumerMessage));
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task DeleteByTag_Deletes_Scheduled_Messages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var consumerMessage1 = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            var consumerMessage2 = new MultiConsumerMessage
            {
                Name = "test-name"
            };

            var scheduledMessageService = serviceProvider.GetRequiredService<IScheduleService>();
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var tag = "test-tag";
            scheduledMessageService.Schedule(consumerMessage1, "* * * * * *", "UTC", tag);
            scheduledMessageService.Schedule(consumerMessage2, "* * * * * *", "UTC", tag);
            await dbContext.SaveChangesAsync();

            // When
            await scheduledMessageService.DeleteByTag("test-tag", CancellationToken.None);
            await dbContext.SaveChangesAsync();

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var count = await postDbContext.ScheduledMessages.CountAsync();
                count.ShouldBe(0);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }


    [Theory]
    [MemberData(nameof(GetTestDbContainers))]
    public async Task DeleteById_Deletes_Scheduled_Messages(TestDbContainerBase dbContainer)
    {
        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };

            var scheduledMessageService = serviceProvider.GetRequiredService<IScheduleService>();
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var id = scheduledMessageService.Schedule(consumerMessage, "* * * * * *", "UTC");
            await dbContext.SaveChangesAsync();

            // When
            await scheduledMessageService.DeleteById(id, CancellationToken.None);
            await dbContext.SaveChangesAsync();

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var count = await postDbContext.ScheduledMessages.CountAsync();
                count.ShouldBe(0);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}