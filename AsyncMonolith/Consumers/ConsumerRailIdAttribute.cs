namespace AsyncMonolith.Consumers;

/// <summary>
/// Consumer rail Id attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerRailIdAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerRailIdAttribute"/> class with the specified execution mode.
    /// </summary>
    /// <param name="railId">Determines the rail that the consumer will be executed on.</param>
    public ConsumerRailIdAttribute(int railId)
    {
        RailId = railId;
    }

    /// <summary>
    /// Determines the rail that the consumer will be executed on.
    /// </summary>
    public int RailId { get; }
}