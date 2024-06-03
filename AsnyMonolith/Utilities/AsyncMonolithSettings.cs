namespace AsnyMonolith.Utilities;

public class AsyncMonolithSettings
{
    public int MaxAttempts { get; set; } = 5;
    public int AttemptDelay { get; set; } = 10;

    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
    };
}