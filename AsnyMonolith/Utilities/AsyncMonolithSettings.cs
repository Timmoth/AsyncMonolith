namespace AsnyMonolith.Utilities;

public class AsyncMonolithSettings
{
    public int MaxAttempts { get; set; } = 5;
    public int AttemptDelay { get; set; } = 10;
    public int ProcessorMaxDelay { get; set; } = 1000;
    public int ProcessorMinDelay { get; set; } = 10;

    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 0
    };
}