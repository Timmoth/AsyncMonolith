using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ConsumerMessageFetcherTests : DbTestsBase
{
    [Theory]
    [InlineData(DbType.Ef)]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.MsSql)]
    [InlineData(DbType.PostgreSql)]
    public async Task Fetch_Returns_Batch_Of_Messages(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var settings = AsyncMonolithSettings.Default;
            var serviceProvider = await Setup(dbContainer, settings);
            var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var producer = serviceProvider.GetRequiredService<IProducerService>();
            var fetcher = serviceProvider.GetRequiredService<IConsumerMessageFetcher>();

            var messages = new List<SingleConsumerMessage>();
            for (var i = 0; i < 2 * settings.ProcessorBatchSize; i++)
            {
                messages.Add(new SingleConsumerMessage
                {
                    Name = "test-name"
                });
            }

            await producer.ProduceList(messages);
            await dbContext.SaveChangesAsync();

            // When
            var dbMessages = await fetcher.Fetch(dbContext.ConsumerMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(),
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