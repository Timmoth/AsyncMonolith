using System.Globalization;
using System.Text.Json;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Cron schedules, the schedule service and the scheduled message processor, against in-memory SQLite.
///     The fake clock starts at 2020-08-31T10:00:00Z (11:00 in London, which is on BST).
/// </summary>
public class SchedulingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static long Utc(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUnixTimeSeconds();

    private static ScheduledMessage Message(string cron, string timezone) => new()
    {
        Id = "id",
        Tag = null,
        AvailableAfter = 0,
        CronExpression = cron,
        CronTimeZone = timezone,
        PayloadType = nameof(SingleConsumerMessage),
        Payload = "{}"
    };

    private static FakeTimeProvider Clock() =>
        new(DateTimeOffset.Parse("2020-08-31T10:00:00Z", CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("UTC", "2020-09-01T09:00:00Z")]
    [InlineData("Europe/London", "2020-09-01T08:00:00Z")]
    [InlineData("America/New_York", "2020-08-31T13:00:00Z")]
    public void Next_occurrence_is_calculated_in_the_schedule_timezone(string timezone, string expected)
    {
        var message = Message("0 0 9 * * *", timezone);

        message.GetNextOccurrence(Clock()).ShouldBe(Utc(expected));
    }

    [Fact]
    public void Next_occurrence_is_strictly_after_now()
    {
        // 10:00:00 matches the expression, but the next occurrence is the following minute.
        Message("0 * * * * *", "UTC").GetNextOccurrence(Clock()).ShouldBe(Utc("2020-08-31T10:01:00Z"));
    }

    [Fact]
    public void Five_field_cron_expressions_are_accepted_with_minute_precision()
    {
        Message("0 9 * * *", "UTC").GetNextOccurrence(Clock()).ShouldBe(Utc("2020-09-01T09:00:00Z"));
        Message("*/15 * * * *", "UTC").GetNextOccurrence(Clock()).ShouldBe(Utc("2020-08-31T10:15:00Z"));
    }

    [Fact]
    public void Six_field_cron_expressions_start_with_seconds()
    {
        Message("30 0 9 * * *", "UTC").GetNextOccurrence(Clock()).ShouldBe(Utc("2020-09-01T09:00:30Z"));
    }

    [Theory]
    [InlineData("0 9 * *")]
    [InlineData("0 0 0 9 * * *")]
    public void Cron_expressions_with_the_wrong_number_of_fields_are_rejected(string expression)
    {
        Should.Throw<CronFormatException>(() => Message(expression, "UTC").GetNextOccurrence(Clock()));
    }

    [Fact]
    public void Unknown_timezone_throws()
    {
        Should.Throw<TimeZoneNotFoundException>(() => Message("0 0 9 * * *", "Not/AZone").GetNextOccurrence(Clock()));
    }

    [Fact]
    public void UpdateSchedule_changes_the_expression_and_recalculates_availability()
    {
        var message = Message("0 0 9 * * *", "UTC");

        message.UpdateSchedule("0 30 10 * * *", "UTC", Clock());

        message.CronExpression.ShouldBe("0 30 10 * * *");
        message.CronTimeZone.ShouldBe("UTC");
        message.AvailableAfter.ShouldBe(Utc("2020-08-31T10:30:00Z"));
    }

    [Fact]
    public void UpdatePayload_replaces_the_payload_and_its_type()
    {
        var message = Message("0 0 9 * * *", "UTC");

        message.UpdatePayload(new OrderedMessage { Name = "updated" });

        message.PayloadType.ShouldBe(nameof(OrderedMessage));
        JsonSerializer.Deserialize<OrderedMessage>(message.Payload)!.Name.ShouldBe("updated");
    }

    [Fact]
    public async Task Schedule_stores_the_message_with_its_first_occurrence()
    {
        await using var host = await SqliteTestHost.Create();

        var id = await host.Schedule(new SingleConsumerMessage { Name = "daily" }, "0 0 9 * * *", "UTC", "reports");

        var stored = (await host.ScheduledMessages()).ShouldHaveSingleItem();
        stored.Id.ShouldBe(id);
        stored.Tag.ShouldBe("reports");
        stored.CronExpression.ShouldBe("0 0 9 * * *");
        stored.CronTimeZone.ShouldBe("UTC");
        stored.PayloadType.ShouldBe(nameof(SingleConsumerMessage));
        JsonSerializer.Deserialize<SingleConsumerMessage>(stored.Payload)!.Name.ShouldBe("daily");
        stored.AvailableAfter.ShouldBe(Utc("2020-09-01T09:00:00Z"));
    }

    [Fact]
    public async Task Schedule_rejects_an_invalid_expression_before_saving()
    {
        await using var host = await SqliteTestHost.Create();

        await Should.ThrowAsync<CronFormatException>(() =>
            host.Schedule(new SingleConsumerMessage { Name = "x" }, "not a cron"));

        (await host.ScheduledMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scheduling_the_same_tag_twice_creates_two_schedules()
    {
        // Documents current behaviour: Schedule always inserts, so registering a schedule
        // at every startup duplicates it unless the caller deletes by tag first.
        await using var host = await SqliteTestHost.Create();

        await host.Schedule(new SingleConsumerMessage { Name = "x" }, "0 0 9 * * *", tag: "nightly");
        await host.Schedule(new SingleConsumerMessage { Name = "x" }, "0 0 9 * * *", tag: "nightly");

        (await host.ScheduledMessages()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteByTag_removes_only_schedules_with_that_tag()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "a" }, "0 0 9 * * *", tag: "remove");
        await host.Schedule(new SingleConsumerMessage { Name = "b" }, "0 0 9 * * *", tag: "remove");
        var keep = await host.Schedule(new SingleConsumerMessage { Name = "c" }, "0 0 9 * * *", tag: "keep");

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IScheduleService>().DeleteByTag("remove", Ct);
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        }

        (await host.ScheduledMessages()).ShouldHaveSingleItem().Id.ShouldBe(keep);
    }

    [Fact]
    public async Task DeleteById_removes_one_schedule_and_ignores_unknown_ids()
    {
        await using var host = await SqliteTestHost.Create();
        var remove = await host.Schedule(new SingleConsumerMessage { Name = "a" }, "0 0 9 * * *");
        var keep = await host.Schedule(new SingleConsumerMessage { Name = "b" }, "0 0 9 * * *");

        using (var scope = host.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IScheduleService>();
            await service.DeleteById(remove, Ct);
            await service.DeleteById("does-not-exist", Ct);
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        }

        (await host.ScheduledMessages()).ShouldHaveSingleItem().Id.ShouldBe(keep);
    }

    [Fact]
    public async Task Due_schedule_produces_a_message_and_moves_to_its_next_occurrence()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "tick" }, "0 * * * * *");

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(0);
        (await host.ConsumerMessages()).ShouldBeEmpty();

        host.Time.Advance(TimeSpan.FromMinutes(1));
        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(1);

        var produced = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        produced.ConsumerType.ShouldBe(nameof(SingleConsumer));
        produced.AvailableAfter.ShouldBe(host.Now);
        JsonSerializer.Deserialize<SingleConsumerMessage>(produced.Payload)!.Name.ShouldBe("tick");
        (await host.ScheduledMessages()).ShouldHaveSingleItem().AvailableAfter
            .ShouldBe(Utc("2020-08-31T10:02:00Z"));

        // Produced messages are consumed by the consumer processor as normal
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task Due_schedule_produces_a_message_for_every_consumer_of_the_payload()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new MultiConsumerMessage { Name = "x" }, "0 * * * * *");
        host.Time.Advance(TimeSpan.FromMinutes(1));

        await host.ScheduledProcessor.ProcessBatch(Ct);

        var produced = await host.ConsumerMessages();
        produced.Select(m => m.ConsumerType).Order()
            .ShouldBe([nameof(MultiConsumer1), nameof(MultiConsumer2)]);
        produced.Select(m => m.DeduplicationKey).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Missed_occurrences_are_not_caught_up()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "tick" }, "0 * * * * *");

        // Five occurrences pass while the processor is not running
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.ScheduledProcessor.ProcessBatch(Ct);
        await host.ScheduledProcessor.ProcessBatch(Ct);

        (await host.ConsumerMessages()).ShouldHaveSingleItem();
        (await host.ScheduledMessages()).ShouldHaveSingleItem().AvailableAfter
            .ShouldBe(Utc("2020-08-31T10:06:00Z"));
    }

    [Fact]
    public async Task Scheduled_processor_respects_the_batch_size()
    {
        await using var host = await SqliteTestHost.Create(s => s.BatchSize = 2);
        for (var i = 0; i < 3; i++)
        {
            await host.Schedule(new SingleConsumerMessage { Name = $"s{i}" }, "0 * * * * *");
        }

        host.Time.Advance(TimeSpan.FromMinutes(1));

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(2);
        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(1);
        (await host.ConsumerMessages()).Count.ShouldBe(3);
    }

    private static ScheduledMessage StoredSchedule(string id, string cron, string timeZone, string payloadType,
        long availableAfter) => new()
    {
        Id = id,
        Tag = id,
        AvailableAfter = availableAfter,
        CronExpression = cron,
        CronTimeZone = timeZone,
        PayloadType = payloadType,
        Payload = "{\"name\":\"x\"}"
    };

    [Theory]
    [InlineData("not a cron", "UTC")]
    [InlineData("0 * * * * *", "Not/AZone")]
    public async Task A_schedule_that_cant_be_evaluated_doesnt_stop_the_others(string cron, string timeZone)
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "good" }, "0 * * * * *");
        await host.Change(db => db.ScheduledMessages.Add(
            StoredSchedule("broken", cron, timeZone, nameof(SingleConsumerMessage), host.Now)));
        host.Time.Advance(TimeSpan.FromMinutes(1));

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(1);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().Payload.ShouldContain("good");
        var broken = (await host.ScheduledMessages()).Single(m => m.Id == "broken");
        broken.AvailableAfter.ShouldBe(host.Now + ScheduledMessageProcessor<TestDbContext>.InvalidScheduleRetrySeconds);

        // And it isn't retried every poll.
        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_schedule_whose_payload_has_no_consumers_skips_the_occurrence_without_stopping_the_others()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Schedule(new SingleConsumerMessage { Name = "good" }, "0 * * * * *");
        await host.Change(db => db.ScheduledMessages.Add(
            StoredSchedule("orphan", "0 * * * * *", "UTC", "RemovedPayload", host.Now)));
        host.Time.Advance(TimeSpan.FromMinutes(1));

        (await host.ScheduledProcessor.ProcessBatch(Ct)).ShouldBe(1);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().Payload.ShouldContain("good");
        (await host.ScheduledMessages()).Single(m => m.Id == "orphan").AvailableAfter
            .ShouldBe(Utc("2020-08-31T10:02:00Z"));
    }
}
