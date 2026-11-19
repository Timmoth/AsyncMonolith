using Microsoft.Extensions.Logging;

namespace AsyncMonolith.Utilities;

/// <summary>
///     Source-generated log messages for the background processors.
/// </summary>
internal static partial class AsyncMonolithLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error processing consumer message batch.")]
    public static partial void ConsumerBatchFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Successfully processed message for consumer: '{ConsumerType}'")]
    public static partial void ConsumerMessageProcessed(this ILogger logger, string consumerType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to consume message on attempt {Attempt}.")]
    public static partial void ConsumerMessageFailed(this ILogger logger, Exception exception, int attempt);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error scheduling next message")]
    public static partial void ScheduleBatchFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Successfully scheduled message")]
    public static partial void ScheduledMessagesProduced(this ILogger logger);
}
