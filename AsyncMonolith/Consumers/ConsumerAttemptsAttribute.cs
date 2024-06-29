namespace AsyncMonolith.Consumers;

/// <summary>
/// Consumer attempts attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerAttemptsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerAttemptsAttribute"/> class with the specified duration.
    /// </summary>
    /// <param name="attempts">The number of attempts before a message is placed in the poisoned table.</param>
    public ConsumerAttemptsAttribute(int attempts)
    {
        Attempts = attempts;
    }

    /// <summary>
    /// The number of attempts before a message is placed in the poisoned table.
    /// </summary>
    public int Attempts { get; }
}
