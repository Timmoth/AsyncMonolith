using AsnyMonolith.Utilities;

namespace AsyncMonolith.Tests.Infra;

public class FakeIdGenerator : IAsyncMonolithIdGenerator
{
    public int Count { get; private set; }

    public string GenerateId()
    {
        return $"fake-id-{Count++}";
    }
}