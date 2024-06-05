using System.Diagnostics;

namespace AsyncMonolith.Utilities;

public static class AsyncMonolithInstrumentation
{
    public const string ActivitySourceName = "async_monolith";
    public const string ProcessConsumerMessageActivity = "process_consumer_message";
    public const string ProcessScheduledMessageActivity = "process_scheduled_message";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}