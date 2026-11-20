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
    /// <param name="seconds">The timeout in seconds, between 1 and 3600.</param>
    public ConsumerTimeoutAttribute(int seconds)
    {
        Seconds = seconds;
    }

    /// <summary>
    /// Gets the duration of the consumer timeout.
    /// </summary>
    public int Seconds { get; }
}
