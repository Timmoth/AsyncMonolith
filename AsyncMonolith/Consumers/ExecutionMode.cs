namespace AsyncMonolith.Consumers;

/// <summary>
/// Determines if multiple consumers of the same type can be executed concurrently on the same processor instance
/// </summary>
public enum ConsumerInstanceExecutionMode
{
    /// <summary>
    /// Will ensure no more than one instance of each consumer will be running concurrently on each processor instance
    /// </summary>
    Sequential,
    /// <summary>
    /// Will allow more than one instance of each consumer to be running concurrently on each processor instance
    /// </summary>
    Parallel
}