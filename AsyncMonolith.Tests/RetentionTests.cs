using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Optional deletion of old poisoned messages.
/// </summary>
public class RetentionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PoisonedMessage Poisoned(string id, long lastDue) => new()
    {
        Id = id, CreatedAt = lastDue, AvailableAfter = lastDue, Attempts = 5, ConsumerType = "c", RailId = 0,
        PayloadType = "p", Payload = "{}", DeduplicationKey = id, TraceId = null, SpanId = null
    };

    private static async Task<SqliteTestHost> HostWithPoisonedMessages(TimeSpan? retention)
    {
        var host = await SqliteTestHost.Create(s => s.PoisonedMessageRetention = retention);
        await host.Change(db =>
        {
            db.PoisonedMessages.Add(Poisoned("old", host.Now - (long)TimeSpan.FromDays(8).TotalSeconds));
            db.PoisonedMessages.Add(Poisoned("recent", host.Now - (long)TimeSpan.FromDays(6).TotalSeconds));
        });
        return host;
    }

    [Fact]
    public async Task Poisoned_messages_older_than_the_retention_are_deleted()
    {
        await using var host = await HostWithPoisonedMessages(TimeSpan.FromDays(7));

        (await host.ScheduledProcessor.CleanUpIfDue(Ct)).ShouldBe(1);

        (await host.PoisonedMessages()).ShouldHaveSingleItem().Id.ShouldBe("recent");
    }

    [Fact]
    public async Task Poisoned_messages_are_kept_when_retention_is_off()
    {
        await using var host = await HostWithPoisonedMessages(null);

        (await host.ScheduledProcessor.CleanUpIfDue(Ct)).ShouldBe(0);

        (await host.PoisonedMessages()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Cleanup_runs_at_most_once_an_hour()
    {
        await using var host = await HostWithPoisonedMessages(TimeSpan.FromDays(7));
        await host.ScheduledProcessor.CleanUpIfDue(Ct);

        // A message that is already expired arrives, but the next cleanup isn't due until the hour is up
        host.Time.Advance(TimeSpan.FromMinutes(59));
        await host.Change(db => db.PoisonedMessages.Add(Poisoned("older", 0)));
        (await host.ScheduledProcessor.CleanUpIfDue(Ct)).ShouldBe(0);

        host.Time.Advance(TimeSpan.FromMinutes(1));
        (await host.ScheduledProcessor.CleanUpIfDue(Ct)).ShouldBe(1);
        (await host.PoisonedMessages()).ShouldHaveSingleItem().Id.ShouldBe("recent");
    }

    [Fact]
    public void Retention_under_an_hour_is_rejected()
    {
        var services = new ServiceCollection();
        services.InternalConfigureAsyncMonolithOptions(o => o.PoisonedMessageRetention = TimeSpan.FromMinutes(30));

        Should.Throw<OptionsValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value);
    }
}
