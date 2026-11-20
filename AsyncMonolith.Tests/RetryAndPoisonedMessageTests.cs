using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Retry backoff, recorded errors and the poisoned message service.
/// </summary>
public class RetryAndPoisonedMessageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    [InlineData(5, 100)]
    [InlineData(1000, 100)]
    public void Retry_delay_doubles_each_attempt_up_to_the_maximum(int attempts, int expectedSeconds)
    {
        new AsyncMonolithOptions { AttemptDelay = TimeSpan.FromSeconds(10), MaxAttemptDelay = TimeSpan.FromSeconds(100) }
            .GetRetryDelay(attempts).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Retry_delay_never_drops_below_the_attempt_delay()
    {
        var options = new AsyncMonolithOptions
            { AttemptDelay = TimeSpan.FromMinutes(10), MaxAttemptDelay = TimeSpan.FromMinutes(5) };

        options.GetRetryDelay(1).ShouldBe(TimeSpan.FromMinutes(10));
        options.GetRetryDelay(3).ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Zero_attempt_delay_retries_immediately()
    {
        new AsyncMonolithOptions { AttemptDelay = TimeSpan.Zero }.GetRetryDelay(5).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task Failed_messages_back_off_between_retries()
    {
        await using var host = await SqliteTestHost.Create(s =>
        {
            s.AttemptDelay = TimeSpan.FromSeconds(10);
            s.MaxAttemptDelay = TimeSpan.FromSeconds(25);
            s.MaxAttempts = 10;
        });
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" });

        var delays = new List<long>();
        for (var i = 0; i < 4; i++)
        {
            var before = host.Now;
            await host.ConsumerProcessor.ProcessBatch(Ct);
            var message = (await host.ConsumerMessages()).ShouldHaveSingleItem();
            delays.Add(message.AvailableAfter - before);
            host.Time.Advance(TimeSpan.FromSeconds(message.AvailableAfter - before));
        }

        delays.ShouldBe([10, 20, 25, 25]);
    }

    [Fact]
    public async Task Failed_attempt_records_the_error()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MixedOutcomeMessage());

        await host.ConsumerProcessor.ProcessBatch(Ct);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().LastError
            .ShouldBe("System.InvalidOperationException: Always fails");
    }

    [Fact]
    public async Task Timed_out_attempt_records_the_timeout()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new TimeoutConsumerMessage { Delay = 5 });

        await host.ConsumerProcessor.ProcessBatch(Ct);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().LastError.ShouldBe("Timed out after 1 seconds.");
    }

    [Fact]
    public async Task Poisoned_message_keeps_the_last_error()
    {
        await using var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        await host.Produce(new MixedOutcomeMessage());

        await host.ConsumerProcessor.ProcessBatch(Ct);

        (await host.PoisonedMessages()).ShouldHaveSingleItem().LastError
            .ShouldBe("System.InvalidOperationException: Always fails");
    }

    private static async Task<SqliteTestHost> HostWithPoisonedMessages(int count)
    {
        var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        for (var i = 0; i < count; i++)
        {
            await host.Produce(new ExceptionConsumerMessage { Name = $"boom{i}" });
            host.Time.Advance(TimeSpan.FromSeconds(1));
        }

        while (await host.ConsumerProcessor.ProcessBatch(Ct) > 0 || (await host.ConsumerMessages()).Count > 0)
        {
        }

        return host;
    }

    private static async Task<T> UseAsync<T>(SqliteTestHost host, Func<IPoisonedMessageService, Task<T>> action)
    {
        using var scope = host.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IPoisonedMessageService>());
    }

    [Fact]
    public async Task Poisoned_messages_are_counted_and_listed_newest_first()
    {
        await using var host = await HostWithPoisonedMessages(3);

        (await UseAsync(host, s => s.Count(Ct))).ShouldBe(3);

        var page = await UseAsync(host, s => s.List(0, 2, Ct));
        page.Count.ShouldBe(2);
        page[0].CreatedAt.ShouldBeGreaterThan(page[1].CreatedAt);
        page.ShouldAllBe(m => m.LastError != null);

        (await UseAsync(host, s => s.List(2, 2, Ct))).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Listing_rejects_invalid_paging()
    {
        await using var host = await SqliteTestHost.Create();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => UseAsync(host, s => s.List(-1, 10, Ct)));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => UseAsync(host, s => s.List(0, 0, Ct)));
    }

    [Fact]
    public async Task Retry_moves_a_poisoned_message_back_to_the_queue()
    {
        await using var host = await HostWithPoisonedMessages(1);
        var poisoned = (await host.PoisonedMessages()).ShouldHaveSingleItem();

        (await UseAsync(host, s => s.Retry(poisoned.Id, Ct))).ShouldBeTrue();

        (await host.PoisonedMessages()).ShouldBeEmpty();
        var requeued = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        requeued.Id.ShouldBe(poisoned.Id);
        requeued.Attempts.ShouldBe(0);
        requeued.AvailableAfter.ShouldBe(host.Now);
        requeued.Payload.ShouldBe(poisoned.Payload);
        requeued.InsertId.ShouldBe(poisoned.InsertId);
        requeued.LastError.ShouldBe(poisoned.LastError);

        // It is processed again (and, since this consumer always fails, poisoned again)
        await host.ConsumerProcessor.ProcessBatch(Ct);
        host.Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(2);
    }

    [Fact]
    public async Task RetryAll_moves_every_poisoned_message_back()
    {
        await using var host = await HostWithPoisonedMessages(3);

        (await UseAsync(host, s => s.RetryAll(Ct))).ShouldBe(3);

        (await host.PoisonedMessages()).ShouldBeEmpty();
        (await host.ConsumerMessages()).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Delete_removes_a_poisoned_message()
    {
        await using var host = await HostWithPoisonedMessages(2);
        var poisoned = await host.PoisonedMessages();

        (await UseAsync(host, s => s.Delete(poisoned[0].Id, Ct))).ShouldBeTrue();

        (await host.PoisonedMessages()).ShouldHaveSingleItem().Id.ShouldBe(poisoned[1].Id);
        (await host.ConsumerMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_ids_are_reported_as_not_found()
    {
        await using var host = await SqliteTestHost.Create();

        (await UseAsync(host, s => s.Retry("missing", Ct))).ShouldBeFalse();
        (await UseAsync(host, s => s.Delete("missing", Ct))).ShouldBeFalse();
    }
}
