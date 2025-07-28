namespace AsyncMonolith.Consumers;

/// <summary>
/// Consumer Instance Execution mode attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerExecutionModeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerExecutionModeAttribute"/> class with the specified execution mode.
    /// </summary>
    /// <param name="executionMode">Determines if multiple consumers of the same type can be executed concurrently on the same processor instance.</param>
    public ConsumerExecutionModeAttribute(ConsumerInstanceExecutionMode executionMode)
    {
        ExecutionMode = executionMode;
    }

    /// <summary>
    /// Determines if multiple consumers of the same type can be executed concurrently on the same processor instance.
    /// </summary>
    public ConsumerInstanceExecutionMode ExecutionMode { get; }
}