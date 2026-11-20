using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

[Trait("Category", "Integration")]
public class ScheduledMessageFetcherTests : DbTestsBase
{
    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public async Task Fetch_Returns_Batch_Of_Messages(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var settings = new AsyncMonolithOptions();
            var serviceProvider = await Setup(dbContainer, settings);
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var fetcher = serviceProvider.GetRequiredService<IScheduledMessageFetcher>();
            var idGenerator = serviceProvider.GetRequiredService<IAsyncMonolithIdGenerator>();

            for (var i = 0; i < 2 * settings.BatchSize; i++)
            {
                dbContext.ScheduledMessages.Add(new ScheduledMessage
                {
                    AvailableAfter = FakeTime.GetUtcNow().ToUnixTimeSeconds(),
                    ChronExpression = "",
                    ChronTimezone = "",
                    Id = idGenerator.GenerateId(),
                    Payload = "",
                    PayloadType = "",
                    Tag = ""
                });
            }

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // When
            var dbMessages = await fetcher.Fetch(dbContext.ScheduledMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(),
                CancellationToken.None);

            // Then
            dbMessages.Count.ShouldBe(settings.BatchSize);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}