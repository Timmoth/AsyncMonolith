using System.Collections.Concurrent;

namespace AsyncMonolith.Tests.Infra;

/// <summary>
///     Counts consumer invocations. Thread-safe because parallel consumers in a batch run concurrently.
/// </summary>
public class TestConsumerInvocations
{
    private readonly ConcurrentDictionary<string, int> _invocationCounts = new();
    private readonly ConcurrentQueue<string> _order = new();

    public IReadOnlyList<string> Order => _order.ToList();

    public void Increment(string consumerName)
    {
        _invocationCounts.AddOrUpdate(consumerName, 1, (_, count) => count + 1);
        _order.Enqueue(consumerName);
    }

    public int GetInvocationCount(string consumerName)
    {
        return _invocationCounts.GetValueOrDefault(consumerName);
    }
}
