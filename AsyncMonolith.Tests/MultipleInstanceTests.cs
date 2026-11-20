using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Several processors (in one app or several instances) working on the same tables.
/// </summary>
public class MultipleInstanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Returns rows read earlier, as a second instance that read just before the first claimed would.</summary>
    private sealed class StaleConsumerFetcher(List<ConsumerMessage> snapshot) : IConsumerMessageFetcher
    {
        public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime, int railId,
            CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class StaleScheduleFetcher(List<ScheduledMessage> snapshot) : IScheduledMessageFetcher
    {
        public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> scheduledMessageSet, long currentTime,
            CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    [Fact]
    public async Task A_message_claimed_by_one_processor_is_not_claimed_by_another_that_read_it_earlier()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "once" });
        var staleRead = await host.ConsumerMessages();

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);

        var otherInstance = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<TestDbContext>>(
            host.Services, new StaleConsumerFetcher(staleRead));
        (await otherInstance.ProcessBatch(Ct)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task A_message_leased_by_one_processor_is_not_claimed_by_another_that_read_it_earlier()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "once" });
        var staleRead = await host.ConsumerMessages();

        // The first processor has claimed it and is still running the consumer.
        (await host.ConsumerProcessor.ClaimBatch(Ct)).ShouldHaveSingleItem();

        var otherInstance = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<TestDbContext>>(
            host.Services, new StaleConsumerFetcher(staleRead));
        (await otherInstance.ClaimBatch(Ct)).ShouldBeEmpty();
        (await host.ConsumerMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task A_schedule_fires_once_per_occurrence_when_two_processors_see_it()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "tick" }, "0 * * * * *");
        host.Time.Advance(TimeSpan.FromMinutes(1));
        var staleRead = await host.ScheduledMessages();

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(1);

        var otherInstance = ActivatorUtilities.CreateInstance<ScheduledMessageProcessor<TestDbContext>>(
            host.Services, new StaleScheduleFetcher(staleRead));
        (await otherInstance.ProcessBatch(Ct)).ShouldBe(0);
        (await host.ConsumerMessages()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Two_running_instances_sharing_a_database_consume_each_message_exactly_once()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asyncmonolith-instances-{Guid.NewGuid():N}.db");
        var instances = new List<IHost>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var builder = Host.CreateApplicationBuilder();
                builder.Services.AddDbContext<TestDbContext>(o => o.UseSqlite($"Data Source={path}"));
                builder.Services.AddSingleton<TestConsumerInvocations>();
                builder.Services.AddSingleton<ConcurrencyProbe>();
                builder.Services.AddEfAsyncMonolith<TestDbContext>(s =>
                {
                    s.RegisterTypesFromAssemblyContaining<MultipleInstanceTests>();
                    s.ProcessorMaxDelay = 20;
                    s.ProcessorMinDelay = 0;
                    s.ProcessorBatchSize = 5;
                });
                instances.Add(builder.Build());
            }

            using (var scope = instances[0].Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                await db.Database.EnsureCreatedAsync(Ct);
                var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();
                for (var i = 0; i < 60; i++)
                {
                    await producer.Produce(new OrderedMessage { Name = $"m{i}" }, cancellationToken: Ct);
                }

                await db.SaveChangesAsync(Ct);
            }

            foreach (var instance in instances)
            {
                await instance.StartAsync(Ct);
            }

            var counters = instances.Select(h => h.Services.GetRequiredService<TestConsumerInvocations>()).ToList();
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (counters.Sum(c => c.Order.Count) < 60 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, Ct);
            }

            // Give a duplicate a chance to show up before checking.
            await Task.Delay(300, Ct);
            var all = counters.SelectMany(c => c.Order).ToList();
            all.Count.ShouldBe(60);
            all.Distinct().Count().ShouldBe(60);
        }
        finally
        {
            foreach (var instance in instances)
            {
                await instance.StopAsync(CancellationToken.None);
                instance.Dispose();
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }
}
