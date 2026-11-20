using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AsyncMonolith.Utilities;

/// <summary>What happened to a claimed message once its consumer finished.</summary>
internal enum MessageOutcome
{
    Consumed,
    Retrying,
    Poisoned,

    /// <summary>The lease had expired and another processor had claimed the message, so nothing was recorded.</summary>
    LeaseLost
}

/// <summary>
///     AsyncMonolith's metrics, on the <see cref="AsyncMonolithInstrumentation.MeterName" /> meter.
/// </summary>
internal sealed class AsyncMonolithMetrics : IDisposable
{
    private readonly bool _ownsMeter;

    public AsyncMonolithMetrics(IMeterFactory? meterFactory)
    {
        _ownsMeter = meterFactory is null;
        Meter = meterFactory?.Create(AsyncMonolithInstrumentation.MeterName) ??
                new Meter(AsyncMonolithInstrumentation.MeterName);

        Produced = Meter.CreateCounter<long>("async_monolith.messages.produced", "{message}",
            "Messages produced, one per consumer.");
        Consumed = Meter.CreateCounter<long>("async_monolith.messages.consumed", "{message}",
            "Messages consumed successfully.");
        Failed = Meter.CreateCounter<long>("async_monolith.messages.failed", "{message}",
            "Failed attempts that will be retried.");
        Poisoned = Meter.CreateCounter<long>("async_monolith.messages.poisoned", "{message}",
            "Messages moved to poisoned_messages after their last attempt failed.");
        ConsumeDuration = Meter.CreateHistogram<double>("async_monolith.consume.duration", "s",
            "How long consumers took, by outcome.");
        Lag = Meter.CreateHistogram<double>("async_monolith.messages.lag", "s",
            "How long a message waited between becoming due and being claimed.");
        SchedulesFired = Meter.CreateCounter<long>("async_monolith.schedules.fired", "{occurrence}",
            "Schedule occurrences that produced a message.");
    }

    public Meter Meter { get; }
    public Counter<long> Produced { get; }
    public Counter<long> Consumed { get; }
    public Counter<long> Failed { get; }
    public Counter<long> Poisoned { get; }
    public Histogram<double> ConsumeDuration { get; }
    public Histogram<double> Lag { get; }
    public Counter<long> SchedulesFired { get; }

    public void RecordOutcome(string consumerType, MessageOutcome outcome, TimeSpan duration)
    {
        var consumer = new KeyValuePair<string, object?>("consumer_type", consumerType);
        switch (outcome)
        {
            case MessageOutcome.Consumed:
                Consumed.Add(1, consumer);
                break;
            case MessageOutcome.Retrying:
                Failed.Add(1, consumer);
                break;
            case MessageOutcome.Poisoned:
                Failed.Add(1, consumer);
                Poisoned.Add(1, consumer);
                break;
            case MessageOutcome.LeaseLost:
                break;
        }

        ConsumeDuration.Record(duration.TotalSeconds, consumer,
            new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant()));
    }

    public void Dispose()
    {
        if (_ownsMeter)
        {
            Meter.Dispose();
        }
    }
}
