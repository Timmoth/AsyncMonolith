namespace AsyncMonolith.Utilities;

public class AsyncMonolithSettings
{
    public int MaxAttempts { get; set; } = 5;
    public int AttemptDelay { get; set; } = 10;
    public int ProcessorMaxDelay { get; set; } = 1000;
    public int ProcessorMinDelay { get; set; } = 10;
    public int ProcessorBatchSize { get; set; } = 5;
    public int ConsumerMessageProcessorCount { get; set; } = 1;
    public int ScheduledMessageProcessorCount { get; set; } = 1;

    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 20,
        ConsumerMessageProcessorCount = 1,
        ScheduledMessageProcessorCount = 1,
        ProcessorBatchSize = 5
    };
}