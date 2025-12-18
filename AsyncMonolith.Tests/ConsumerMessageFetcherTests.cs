using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ConsumerMessageFetcherTests : DbTestsBase
{
    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
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
            var dbMessages = await fetcher.Fetch(dbContext.ConsumerMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(), 0,
                CancellationToken.None);

            // Then
            dbMessages.Count.ShouldBe(settings.ProcessorBatchSize);
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
    
    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public async Task Fetch_Returns_Messages_Partitioned_By_Rail(DbType dbType)
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

            await producer.Produce(new RailConsumerMessage() { });
            await dbContext.SaveChangesAsync();

            // When
            var rail0Messages = await fetcher.Fetch(dbContext.ConsumerMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(), 0,
                CancellationToken.None);
            
            var rail1Messages = await fetcher.Fetch(dbContext.ConsumerMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(), 1,
                CancellationToken.None);
            
            var rail2Messages = await fetcher.Fetch(dbContext.ConsumerMessages, FakeTime.GetUtcNow().ToUnixTimeSeconds(), 2,
                CancellationToken.None);

            // Then
            rail0Messages.Count.ShouldBe(1);
            rail1Messages.Count.ShouldBe(1);
            rail2Messages.Count.ShouldBe(1);

            rail0Messages.Single().RailId.ShouldBe(0);
            rail1Messages.Single().RailId.ShouldBe(1);
            rail2Messages.Single().RailId.ShouldBe(2);

            rail0Messages.Single().ConsumerType.ShouldBe(nameof(RailConsumer0));
            rail1Messages.Single().ConsumerType.ShouldBe(nameof(RailConsumer1));
            rail2Messages.Single().ConsumerType.ShouldBe(nameof(RailConsumer2));
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}