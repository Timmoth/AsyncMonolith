using System.Diagnostics;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Behaviour in the cases production finds: outages, lost leases, tracing, odd schedules and mapping mistakes.
/// </summary>
public class RobustnessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consuming_continues_the_producers_trace()
    {
        var started = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name is "AsyncMonolith.Tests.Trace" or AsyncMonolithInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => { lock (started) { started.Add(a); } }
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("AsyncMonolith.Tests.Trace");
        await using var host = await SqliteTestHost.Create();

        ActivityTraceId traceId;
        ActivitySpanId spanId;
        using (var request = source.StartActivity("request")!)
        {
            traceId = request.TraceId;
            spanId = request.SpanId;
            await host.Produce(new SingleConsumerMessage { Name = "traced" });
        }

        await host.ConsumerProcessor.ProcessBatch(Ct);

        Activity consume;
        lock (started)
        {
            consume = started.Single(a => a.OperationName == AsyncMonolithInstrumentation.ProcessConsumerMessageActivity &&
                                          a.TraceId == traceId);
        }

        consume.ParentSpanId.ShouldBe(spanId);
    }

    [Fact]
    public async Task A_running_host_recovers_once_its_database_is_available()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asyncmonolith-outage-{Guid.NewGuid():N}.db");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseSqlite($"Data Source={path}"));
        builder.Services.AddSingleton<TestConsumerInvocations>();
        builder.Services.AddAsyncMonolith<TestDbContext>(o =>
        {
            o.RegisterTypesFromAssemblyContaining<RobustnessTests>();
            o.IdlePollInterval = TimeSpan.FromMilliseconds(50);
            o.BusyPollInterval = TimeSpan.Zero;
        });
        using var host = builder.Build();
        try
        {
            // The tables don't exist yet, so every batch fails while the host runs.
            await host.StartAsync(Ct);
            await Task.Delay(500, Ct);

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                await db.Database.EnsureCreatedAsync(Ct);
                await scope.ServiceProvider.GetRequiredService<IProducerService>()
                    .Produce(new SingleConsumerMessage { Name = "after outage" }, cancellationToken: Ct);
                await db.SaveChangesAsync(Ct);
            }

            var invocations = host.Services.GetRequiredService<TestConsumerInvocations>();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (invocations.GetInvocationCount(nameof(SingleConsumer)) == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, Ct);
            }

            invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_message_whose_lease_was_lost_is_not_poisoned_by_the_old_processor()
    {
        await using var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" });
        var claimed = (await host.ConsumerProcessor.ClaimBatch(Ct)).ShouldHaveSingleItem();
        await host.Change(db => db.ConsumerMessages.Single().AvailableAfter = claimed.Message.AvailableAfter + 100);

        var outcome = await host.ConsumerProcessor.Complete(claimed, success: false, Ct, "failed");

        outcome.ShouldBe(MessageOutcome.LeaseLost);
        (await host.PoisonedMessages()).ShouldBeEmpty();
        (await host.ConsumerMessages()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Scheduling_an_expression_that_never_occurs_is_rejected()
    {
        await using var host = await SqliteTestHost.Create();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            host.Schedule(new SingleConsumerMessage { Name = "x" }, "0 0 30 2 *"));

        exception.Message.ShouldContain("never occurs");
    }

    [Fact]
    public async Task A_stored_schedule_that_never_occurs_is_set_aside_like_any_invalid_schedule()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Change(db => db.ScheduledMessages.Add(new Scheduling.ScheduledMessage
        {
            Id = "never", Tag = null, AvailableAfter = host.Now, CronExpression = "0 0 30 2 *", CronTimeZone = "UTC",
            PayloadType = nameof(SingleConsumerMessage), Payload = "{}"
        }));

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(0);

        (await host.ScheduledMessages()).ShouldHaveSingleItem().AvailableAfter
            .ShouldBe(host.Now + Scheduling.ScheduledMessageProcessor<TestDbContext>.InvalidScheduleRetrySeconds);
    }

    [Fact]
    public void The_builder_adds_consumers_from_more_assemblies()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(_ => { });

        services.AddAsyncMonolith<TestDbContext>().AddConsumersFromAssemblyContaining<RobustnessTests>();

        services.ShouldContain(d => d.ServiceType == typeof(SingleConsumer));
        services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>()
            .ResolvePayloadConsumerTypes(nameof(SingleConsumerMessage)).ShouldBe([nameof(SingleConsumer)]);
    }

    [Fact]
    public async Task Table_names_are_quoted_and_follow_the_mapping()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var plain = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
        await using var prefixed = new PrefixedDbContext(new DbContextOptionsBuilder<PrefixedDbContext>().UseSqlite(connection).Options);

        AsyncMonolithTables.Name(plain.Set<ConsumerMessage>()).ShouldBe("\"consumer_messages\"");
        AsyncMonolithTables.Name(prefixed.Set<Scheduling.ScheduledMessage>()).ShouldBe("\"jobs_scheduled_messages\"");
    }

    private sealed class UnmappedDbContext(DbContextOptions<UnmappedDbContext> options) : DbContext(options);

    [Fact]
    public async Task Forgetting_ConfigureAsyncMonolith_gives_a_clear_error()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = new UnmappedDbContext(new DbContextOptionsBuilder<UnmappedDbContext>().UseSqlite(connection).Options);

        Should.Throw<InvalidOperationException>(() => AsyncMonolithTables.Name(db.Set<ConsumerMessage>()))
            .Message.ShouldContain("ConfigureAsyncMonolith");
    }
}
