using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

/// <summary>Records the payload name, so tests can check which messages ran and in what order.</summary>
public class OrderedMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}

public class OrderedConsumer(TestConsumerInvocations invocations) : IConsumer<OrderedMessage>
{
    public Task Consume(OrderedMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        invocations.Increment($"{nameof(OrderedConsumer)}:{payload.Name}");
        return Task.CompletedTask;
    }
}

/// <summary>One payload, two consumers: one always succeeds, one always fails.</summary>
public class MixedOutcomeMessage : IConsumerPayload
{
}

public class MixedSuccessConsumer(TestConsumerInvocations invocations) : IConsumer<MixedOutcomeMessage>
{
    public Task Consume(MixedOutcomeMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        invocations.Increment(nameof(MixedSuccessConsumer));
        return Task.CompletedTask;
    }
}

public class MixedFailureConsumer(TestConsumerInvocations invocations) : IConsumer<MixedOutcomeMessage>
{
    public Task Consume(MixedOutcomeMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        invocations.Increment(nameof(MixedFailureConsumer));
        throw new InvalidOperationException("Always fails");
    }
}

/// <summary>Non-positive attribute values fall back to the configured defaults.</summary>
public class InvalidAttributesMessage : IConsumerPayload
{
}

[ConsumerTimeout(0)]
[ConsumerAttempts(-1)]
public class InvalidAttributesConsumer : IConsumer<InvalidAttributesMessage>
{
    public Task Consume(InvalidAttributesMessage payload, ConsumeContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

/// <summary>Tracks how many consumers are running at once.</summary>
public sealed class ConcurrencyProbe
{
    private int _current;
    private int _max;
    private readonly TaskCompletionSource _bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Max => Volatile.Read(ref _max);

    public async Task Enter(int expectedConcurrency, CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref _current);
        int max;
        do
        {
            max = Volatile.Read(ref _max);
        } while (current > max && Interlocked.CompareExchange(ref _max, current, max) != max);

        if (current >= expectedConcurrency)
        {
            _bothStarted.TrySetResult();
        }

        // Wait briefly for the other consumer; sequential execution never gets here twice at once.
        await Task.WhenAny(_bothStarted.Task, Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken));
        Interlocked.Decrement(ref _current);
    }
}

public class ParallelProbeMessage : IConsumerPayload
{
}

[ConsumerExecutionMode(ConsumerExecutionMode.Parallel)]
public class ParallelProbeConsumer(ConcurrencyProbe probe) : IConsumer<ParallelProbeMessage>
{
    public Task Consume(ParallelProbeMessage payload, ConsumeContext context, CancellationToken cancellationToken) =>
        probe.Enter(2, cancellationToken);
}

public class SequentialProbeMessage : IConsumerPayload
{
}

[ConsumerExecutionMode(ConsumerExecutionMode.Sequential)]
public class SequentialProbeConsumer(ConcurrencyProbe probe) : IConsumer<SequentialProbeMessage>
{
    public Task Consume(SequentialProbeMessage payload, ConsumeContext context, CancellationToken cancellationToken) =>
        probe.Enter(2, cancellationToken);
}
