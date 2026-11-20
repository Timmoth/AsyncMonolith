using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The IConsumer&lt;T&gt; contract and the ConsumeContext passed to consumers.
/// </summary>
public class ConsumeContextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(SqliteTestHost Host, ContextRecorder Recorder)> Host()
    {
        var host = await SqliteTestHost.Create(s => s.AttemptDelay = TimeSpan.FromSeconds(0));
        return (host, host.Services.GetRequiredService<ContextRecorder>());
    }

    [Fact]
    public async Task Context_describes_the_message_and_attempt()
    {
        var (host, recorder) = await Host();
        await using var _ = host;
        await host.Produce(new ContextMessage(), deduplicationKey: "order-42");
        var stored = (await host.ConsumerMessages()).ShouldHaveSingleItem();

        await host.ConsumerProcessor.ProcessBatch(Ct);

        var context = recorder.Contexts.ShouldHaveSingleItem();
        context.MessageId.ShouldBe(stored.Id);
        context.DeduplicationKey.ShouldBe("order-42");
        context.Attempt.ShouldBe(1);
        context.MaxAttempts.ShouldBe(3);
        context.IsLastAttempt.ShouldBeFalse();
        context.CreatedAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(stored.CreatedAt));
        context.ConsumerName.ShouldBe(nameof(ContextConsumer));
        context.PayloadName.ShouldBe(nameof(ContextMessage));
    }

    [Fact]
    public async Task Attempt_counts_up_across_retries_until_the_last()
    {
        var (host, recorder) = await Host();
        await using var _ = host;
        recorder.FailuresLeft = 2;
        await host.Produce(new ContextMessage());

        for (var i = 0; i < 3; i++)
        {
            await host.ConsumerProcessor.ProcessBatch(Ct);
        }

        recorder.Contexts.Select(c => c.Attempt).ShouldBe([1, 2, 3]);
        recorder.Contexts.Select(c => c.IsLastAttempt).ShouldBe([false, false, true]);
        recorder.Contexts.Select(c => c.MessageId).Distinct().ShouldHaveSingleItem();
        (await host.ConsumerMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Consumers_of_one_payload_share_its_deduplication_key()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MultiConsumerMessage { Name = "x" });

        (await host.ConsumerMessages()).Select(m => m.DeduplicationKey).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public void A_consumer_class_handling_two_payloads_is_rejected_at_startup()
    {
        var settings = new AsyncMonolithOptions();
        settings.RegisterTypesFromAssemblies(ClashingTypes.ConsumerOfTwoPayloads());
        var services = new ServiceCollection();

        var exception = Should.Throw<InvalidOperationException>(() =>
        {
            services.InternalRegisterAsyncMonolithConsumers(settings);
            services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>();
        });

        exception.Message.ShouldContain("TwoPayloads.BothConsumer");
        exception.Message.ShouldContain("one consumer class per payload");
    }
}
