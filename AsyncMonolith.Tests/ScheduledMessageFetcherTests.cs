using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ScheduledMessageFetcherTests : DbTestsBase
{
    [Theory]
    [InlineData(DbType.Ef)]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.MsSql)]
    [InlineData(DbType.PostgreSql)]
    [InlineData(DbType.MariaDb)]
    public async Task Fetch_Returns_Batch_Of_Messages(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var settings = AsyncMonolithSettings.Default;
            var serviceProvider = await Setup(dbContainer, settings);
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var fetcher = serviceProvider.GetRequiredService<IScheduledMessageFetcher>();
            var idGenerator = serviceProvider.GetRequiredService<IAsyncMonolithIdGenerator>();

            for (var i = 0; i < 2 * settings.ProcessorBatchSize; i++)
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

            await dbContext.SaveChangesAsync();

            // When
            var dbMessages = await fetcher.Fetch(dbContext.ScheduledMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(),
                CancellationToken.None);

            // Then
            dbMessages.Count.Should().Be(settings.ProcessorBatchSize);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}