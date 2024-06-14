namespace AsyncMonolith.Utilities;

/// <summary>
///     Represents the settings for the AsyncMonolith application.
/// </summary>
public class AsyncMonolithSettings
{
    /// <summary>
    ///     Gets or sets the maximum number of attempts for processing a message.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the delay in seconds between attempts for processing a failed message.
    /// </summary>
    public int AttemptDelay { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the maximum delay in milliseconds between processor cycles.
    /// </summary>
    public int ProcessorMaxDelay { get; set; } = 1000;

    /// <summary>
    ///     Gets or sets the minimum delay in milliseconds between processor cycles.
    /// </summary>
    public int ProcessorMinDelay { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the number of messages to process in a batch.
    /// </summary>
    public int ProcessorBatchSize { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the number of consumer message processors to be ran for each app instance.
    /// </summary>
    public int ConsumerMessageProcessorCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the number of scheduled message processors to be ran for each app instance.
    /// </summary>
    public int ScheduledMessageProcessorCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the default number of seconds a consumer waits before timing out.
    /// </summary>
    public int DefaultConsumerTimeout { get; set; } = 10;

    /// <summary>
    ///     Gets the default AsyncMonolithSettings.
    /// </summary>
    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 10,
        ConsumerMessageProcessorCount = 1,
        ScheduledMessageProcessorCount = 1,
        ProcessorBatchSize = 5
    };
}