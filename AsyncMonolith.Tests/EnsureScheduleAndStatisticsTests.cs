using System.Globalization;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     EnsureSchedule (idempotent schedules) and queue statistics.
/// </summary>
public class EnsureScheduleAndStatisticsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static long Utc(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUnixTimeSeconds();

    private static async Task<string> Ensure(SqliteTestHost host, string name, string cron, string tag = "nightly",
        string timezone = "UTC")
    {
        using var scope = host.Services.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<IScheduleService>()
            .EnsureSchedule(new SingleConsumerMessage { Name = name }, cron, timezone, tag, Ct);
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        return id;
    }

    private static async Task<QueueStatistics> Statistics(SqliteTestHost host)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQueueStatisticsService>().Read(Ct);
    }

    [Fact]
    public async Task EnsureSchedule_creates_the_schedule_when_missing()
    {
        await using var host = await SqliteTestHost.Create();

        var id = await Ensure(host, "a", "0 0 9 * * *");

        var schedule = (await host.ScheduledMessages()).ShouldHaveSingleItem();
        schedule.Id.ShouldBe(id);
        schedule.Tag.ShouldBe("nightly");
        schedule.AvailableAfter.ShouldBe(Utc("2020-09-01T09:00:00Z"));
    }

    [Fact]
    public async Task EnsureSchedule_at_every_startup_keeps_one_schedule_and_its_next_occurrence()
    {
        await using var host = await SqliteTestHost.Create();
        var id = await Ensure(host, "a", "0 0 9 * * *");

        // A restart a few hours later: the next occurrence must not move.
        host.Time.Advance(TimeSpan.FromHours(3));
        (await Ensure(host, "a", "0 0 9 * * *")).ShouldBe(id);

        var schedule = (await host.ScheduledMessages()).ShouldHaveSingleItem();
        schedule.AvailableAfter.ShouldBe(Utc("2020-09-01T09:00:00Z"));
    }

    [Fact]
    public async Task EnsureSchedule_updates_the_payload()
    {
        await using var host = await SqliteTestHost.Create();
        await Ensure(host, "old", "0 0 9 * * *");

        await Ensure(host, "new", "0 0 9 * * *");

        JsonSerializer.Deserialize<SingleConsumerMessage>((await host.ScheduledMessages()).ShouldHaveSingleItem()
            .Payload)!.Name.ShouldBe("new");
    }

    [Fact]
    public async Task EnsureSchedule_recalculates_when_the_expression_or_timezone_changes()
    {
        await using var host = await SqliteTestHost.Create();
        await Ensure(host, "a", "0 0 9 * * *");

        await Ensure(host, "a", "0 30 10 * * *");
        (await host.ScheduledMessages()).ShouldHaveSingleItem().AvailableAfter.ShouldBe(Utc("2020-08-31T10:30:00Z"));

        await Ensure(host, "a", "0 30 10 * * *", timezone: "Europe/London");
        var schedule = (await host.ScheduledMessages()).ShouldHaveSingleItem();
        schedule.ChronTimezone.ShouldBe("Europe/London");
        schedule.AvailableAfter.ShouldBe(Utc("2020-09-01T09:30:00Z"));
    }

    [Fact]
    public async Task EnsureSchedule_removes_duplicates_left_by_Schedule()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "a" }, "0 0 9 * * *", tag: "nightly");
        await host.Schedule(new SingleConsumerMessage { Name = "a" }, "0 0 9 * * *", tag: "nightly");
        await host.Schedule(new SingleConsumerMessage { Name = "other" }, "0 0 9 * * *", tag: "other");

        await Ensure(host, "a", "0 0 9 * * *");

        (await host.ScheduledMessages()).Select(m => m.Tag).Order().ShouldBe(["nightly", "other"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EnsureSchedule_requires_a_tag(string tag)
    {
        await using var host = await SqliteTestHost.Create();

        await Should.ThrowAsync<ArgumentException>(() => Ensure(host, "a", "0 0 9 * * *", tag));
    }

    [Fact]
    public async Task FakeScheduleService_EnsureSchedule_replaces_the_recorded_schedule()
    {
        var fake = new FakeScheduleService(TimeProvider.System, new FakeIdGenerator());

        await fake.EnsureSchedule(new SingleConsumerMessage { Name = "a" }, "0 0 9 * * *", "UTC", "nightly", Ct);
        await fake.EnsureSchedule(new SingleConsumerMessage { Name = "b" }, "0 0 9 * * *", "UTC", "nightly", Ct);

        JsonSerializer.Deserialize<SingleConsumerMessage>(fake.CreatedScheduledMessages.ShouldHaveSingleItem().Payload)!
            .Name.ShouldBe("b");
    }

    [Fact]
    public async Task Statistics_are_zero_for_an_empty_queue()
    {
        await using var host = await SqliteTestHost.Create();

        (await Statistics(host)).ShouldBe(new QueueStatistics(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Statistics_count_due_waiting_poisoned_and_scheduled_messages()
    {
        await using var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        await host.Produce(new SingleConsumerMessage { Name = "due-first" });
        host.Time.Advance(TimeSpan.FromSeconds(30));
        await host.Produce(new SingleConsumerMessage { Name = "due-second" });
        await host.Produce(new SingleConsumerMessage { Name = "later" }, host.Now + 600);
        await host.Schedule(new SingleConsumerMessage { Name = "s" }, "0 0 9 * * *");
        await host.Change(db => db.PoisonedMessages.Add(new PoisonedMessage
        {
            Id = "p", CreatedAt = 0, AvailableAfter = 0, Attempts = 1, ConsumerType = "c", RailId = 0,
            PayloadType = "p", Payload = "{}", InsertId = "i", TraceId = null, SpanId = null
        }));
        host.Time.Advance(TimeSpan.FromSeconds(15));

        var statistics = await Statistics(host);

        statistics.Available.ShouldBe(2);
        statistics.Waiting.ShouldBe(1);
        statistics.OldestAvailableAgeSeconds.ShouldBe(45);
        statistics.Poisoned.ShouldBe(1);
        statistics.Schedules.ShouldBe(1);
    }

    [Fact]
    public async Task Leased_messages_count_as_waiting()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "a" });

        await host.ConsumerProcessor.ClaimBatch(Ct);

        var statistics = await Statistics(host);
        statistics.Available.ShouldBe(0);
        statistics.Waiting.ShouldBe(1);
    }
}
