using System.Diagnostics;
using System.Text.Json;

namespace AsyncMonolith.Utilities;

/// <summary>
/// Provides instrumentation for the AsyncMonolith application.
/// </summary>
public static class AsyncMonolithInstrumentation
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public const string ActivitySourceName = "async_monolith";

    /// <summary>
    /// The name of the meter that publishes AsyncMonolith's metrics, for example with OpenTelemetry's
    /// <c>AddMeter(AsyncMonolithInstrumentation.MeterName)</c>.
    /// </summary>
    public const string MeterName = "async_monolith";

    /// <summary>
    /// The activity name for processing consumer messages.
    /// </summary>
    public const string ProcessConsumerMessageActivity = "process_consumer_message";

    /// <summary>
    /// The activity name for processing scheduled messages.
    /// </summary>
    public const string ProcessScheduledMessageActivity = "process_scheduled_message";

    /// <summary>
    /// The activity source instance.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
