namespace AsyncMonolith.Utilities;

public enum DbType
{
    Ef = 0,
    PostgreSql = 1,
    MySql = 2
}

public class AsyncMonolithSettings
{
    public int MaxAttempts { get; set; } = 5;
    public int AttemptDelay { get; set; } = 10;
    public int ProcessorMaxDelay { get; set; } = 1000;
    public int ProcessorMinDelay { get; set; } = 10;
    public DbType DbType { get; set; } = DbType.Ef;

    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 0
    };
}