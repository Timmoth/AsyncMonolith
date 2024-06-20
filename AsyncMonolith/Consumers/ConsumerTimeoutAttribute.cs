namespace AsyncMonolith.Consumers;

/// <summary>
/// Consumer timeout attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerTimeoutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerTimeoutAttribute"/> class with the specified duration.
    /// </summary>
    /// <param name="duration">The duration of the consumer timeout.</param>
    public ConsumerTimeoutAttribute(int duration)
    {
        Duration = duration;
    }

    /// <summary>
    /// Gets the duration of the consumer timeout.
    /// </summary>
    public int Duration { get; }
}
