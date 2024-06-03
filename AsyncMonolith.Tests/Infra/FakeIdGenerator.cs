using AsnyMonolith.Utilities;

namespace AsyncMonolith.Tests.Infra;

public class FakeIdGenerator : IAsnyMonolithIdGenerator
{
    public int Count { get; private set; }

    public string GenerateId()
    {
        return $"fake-id-{Count++}";
    }
}