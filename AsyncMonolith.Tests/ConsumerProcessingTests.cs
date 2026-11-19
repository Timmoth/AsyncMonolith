using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Behaviour of <see cref="ConsumerMessageProcessor{T}" /> for one batch, against in-memory SQLite.
/// </summary>
public class ConsumerProcessingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Successful_message_is_consumed_and_removed()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "a" });

        var processed = await host.ConsumerProcessor.ProcessBatch(Ct);

        processed.ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
        (await host.ConsumerMessages()).ShouldBeEmpty();
        (await host.PoisonedMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Empty_queue_processes_nothing()
    {
        await using var host = await SqliteTestHost.Create();

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Message_is_not_consumed_before_it_is_available()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "later" }, host.Now + 60);

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(0);

        host.Time.Advance(TimeSpan.FromSeconds(60));

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task Failed_message_is_retried_after_the_attempt_delay()
    {
        await using var host = await SqliteTestHost.Create(s => s.AttemptDelay = 30);
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" });
        var start = host.Now;

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);

        var message = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        message.Attempts.ShouldBe(1);
        message.AvailableAfter.ShouldBe(start + 30);

        // Not retried before the delay has passed
        host.Time.Advance(TimeSpan.FromSeconds(29));
        await host.ConsumerProcessor.ProcessBatch(Ct);
        host.Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(1);

        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.ConsumerProcessor.ProcessBatch(Ct);
        host.Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(2);
        (await host.ConsumerMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Message_is_poisoned_after_the_configured_max_attempts()
    {
        await using var host = await SqliteTestHost.Create(s =>
        {
            s.MaxAttempts = 3;
            s.AttemptDelay = 0;
        });
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" }, insertId: "insert-1");
        var original = (await host.ConsumerMessages()).ShouldHaveSingleItem();

        for (var i = 0; i < 3; i++)
        {
            await host.ConsumerProcessor.ProcessBatch(Ct);
        }

        host.Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(3);
        (await host.ConsumerMessages()).ShouldBeEmpty();

        var poisoned = (await host.PoisonedMessages()).ShouldHaveSingleItem();
        poisoned.Id.ShouldBe(original.Id);
        poisoned.Attempts.ShouldBe(3);
        poisoned.ConsumerType.ShouldBe(nameof(ExceptionConsumer));
        poisoned.PayloadType.ShouldBe(nameof(ExceptionConsumerMessage));
        poisoned.Payload.ShouldBe(original.Payload);
        poisoned.InsertId.ShouldBe("insert-1");
        poisoned.CreatedAt.ShouldBe(original.CreatedAt);
        poisoned.RailId.ShouldBe(original.RailId);

        // Poisoned messages are never picked up again
        await host.ConsumerProcessor.ProcessBatch(Ct);
        host.Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(3);
    }

    [Fact]
    public async Task Consumer_attempts_attribute_overrides_the_default_max_attempts()
    {
        await using var host = await SqliteTestHost.Create(s =>
        {
            s.MaxAttempts = 10;
            s.AttemptDelay = 0;
        });
        await host.Produce(new ExceptionConsumer2AttemptsMessage { Name = "boom" });

        await host.ConsumerProcessor.ProcessBatch(Ct);
        (await host.PoisonedMessages()).ShouldBeEmpty();

        await host.ConsumerProcessor.ProcessBatch(Ct);
        (await host.PoisonedMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Max_attempts_of_one_poisons_on_the_first_failure()
    {
        await using var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" });

        await host.ConsumerProcessor.ProcessBatch(Ct);

        (await host.ConsumerMessages()).ShouldBeEmpty();
        (await host.PoisonedMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Consumer_that_exceeds_its_timeout_counts_as_a_failed_attempt()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new TimeoutConsumerMessage { Delay = 5 });

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);

        host.Invocations.GetInvocationCount(nameof(TimeoutConsumer)).ShouldBe(1);
        (await host.ConsumerMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Batch_size_limits_how_many_messages_are_processed_at_once()
    {
        await using var host = await SqliteTestHost.Create(s => s.ProcessorBatchSize = 2);
        for (var i = 0; i < 5; i++)
        {
            await host.Produce(new SingleConsumerMessage { Name = $"m{i}" });
        }

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(2);
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(2);
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(5);
    }

    [Fact]
    public async Task Oldest_messages_are_processed_first()
    {
        await using var host = await SqliteTestHost.Create(s => s.ProcessorBatchSize = 1);
        await host.Produce(new OrderedMessage { Name = "newer" });
        await host.Produce(new OrderedMessage { Name = "older" });

        // Make the second message the oldest, so order can't come from insertion or id order.
        await host.Change(db =>
        {
            var messages = db.ConsumerMessages.ToList();
            messages.Single(m => m.Payload.Contains("newer", StringComparison.Ordinal)).CreatedAt = host.Now + 10;
        });

        await host.ConsumerProcessor.ProcessBatch(Ct);
        await host.ConsumerProcessor.ProcessBatch(Ct);

        host.Invocations.Order.ShouldBe(["OrderedConsumer:older", "OrderedConsumer:newer"]);
    }

    [Fact]
    public async Task Each_consumer_of_a_payload_succeeds_or_fails_independently()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MixedOutcomeMessage());

        var processed = await host.ConsumerProcessor.ProcessBatch(Ct);

        processed.ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(MixedSuccessConsumer)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(MixedFailureConsumer)).ShouldBe(1);
        var remaining = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        remaining.ConsumerType.ShouldBe(nameof(MixedFailureConsumer));
        remaining.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Processor_only_takes_messages_for_its_own_rail()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new RailConsumerMessage());

        // The default processor runs rail 0
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(RailConsumer0)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(RailConsumer1)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(RailConsumer2)).ShouldBe(0);

        var rail1 = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<TestDbContext>>(host.Services);
        rail1.SetRailId(1);
        (await rail1.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(RailConsumer1)).ShouldBe(1);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().ConsumerType.ShouldBe(nameof(RailConsumer2));
    }

    [Fact]
    public async Task Unknown_consumer_type_counts_as_a_failed_attempt()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "a" });
        await host.Change(db => db.ConsumerMessages.Single().ConsumerType = "RemovedConsumer");

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Payload_that_deserializes_to_null_counts_as_a_failed_attempt()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "a" });
        await host.Change(db => db.ConsumerMessages.Single().Payload = "null");

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);

        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(0);
        (await host.ConsumerMessages()).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Parallel_consumers_in_a_batch_run_concurrently()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new ParallelProbeMessage());
        await host.Produce(new ParallelProbeMessage());

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(2);

        host.Services.GetRequiredService<ConcurrencyProbe>().Max.ShouldBe(2);
    }

    [Fact]
    public async Task Sequential_consumers_in_a_batch_run_one_at_a_time()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SequentialProbeMessage());
        await host.Produce(new SequentialProbeMessage());

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(2);

        host.Services.GetRequiredService<ConcurrencyProbe>().Max.ShouldBe(1);
    }

    [Fact]
    public async Task Consumer_that_writes_to_the_database_succeeds_on_sqlite()
    {
        // Each DbContext has its own connection, as in a real host. The batch must not hold the
        // database's write lock while consumers run.
        await using var host = await SqliteTestHost.Create(fileDatabase: true);
        await host.Produce(new WritingConsumerMessage { Name = "follow-up" });

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);

        (await host.PoisonedMessages()).ShouldBeEmpty();
        (await host.ConsumerMessages()).ShouldHaveSingleItem().ConsumerType.ShouldBe(nameof(SingleConsumer));
    }

    [Fact]
    public async Task Claimed_message_is_leased_so_other_processors_skip_it()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new TimeoutConsumerMessage { Delay = 0 });

        // Simulate a processor that claimed the message and then crashed before finishing it.
        var claimed = await host.ConsumerProcessor.ClaimBatch(Ct);
        claimed.ShouldHaveSingleItem().Attempts.ShouldBe(1);

        // While the lease (consumer timeout + grace period) is held, nobody else picks it up.
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(TimeoutConsumer)).ShouldBe(0);

        // Once the lease expires, the message is processed again and the crashed attempt counts.
        host.Time.Advance(TimeSpan.FromSeconds(1 + ConsumerMessageProcessor<TestDbContext>.LeaseGracePeriodSeconds));
        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(TimeoutConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task Completing_a_message_whose_lease_was_taken_over_leaves_it_alone()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "a" });
        var claimed = (await host.ConsumerProcessor.ClaimBatch(Ct)).ShouldHaveSingleItem();

        // Another processor re-claimed the message after our lease expired.
        await host.Change(db => db.ConsumerMessages.Single().AvailableAfter = claimed.AvailableAfter + 100);

        await host.ConsumerProcessor.Complete(claimed, success: true, Ct);

        (await host.ConsumerMessages()).ShouldHaveSingleItem();
    }
}
