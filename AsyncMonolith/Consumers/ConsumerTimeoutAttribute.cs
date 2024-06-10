namespace AsyncMonolith.Consumers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ConsumerTimeoutAttribute : Attribute
{
    public ConsumerTimeoutAttribute(int duration)
    {
        Duration = duration;
    }

    public int Duration { get; }
}