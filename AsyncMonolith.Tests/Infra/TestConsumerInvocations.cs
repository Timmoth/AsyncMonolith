namespace AsyncMonolith.Tests.Infra;

public class TestConsumerInvocations
{
    public Dictionary<string, int> InvocationCounts { get; } = new();

    public void Increment(string consumerName)
    {
        if (InvocationCounts.TryGetValue(consumerName, out var count))
            InvocationCounts[consumerName] = count + 1;
        else
            InvocationCounts[consumerName] = 1;
    }

    public int GetInvocationCount(string consumerName)
    {
        return InvocationCounts.TryGetValue(consumerName, out var count) ? count : 0;
    }
}