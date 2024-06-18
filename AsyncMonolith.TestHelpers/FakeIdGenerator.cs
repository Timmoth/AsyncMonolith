using AsyncMonolith.Utilities;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Represents a fake ID generator for testing purposes.
/// </summary>
public class FakeIdGenerator : IAsyncMonolithIdGenerator
{
    /// <summary>
    /// Gets or sets the count of generated IDs.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Generates a fake ID.
    /// </summary>
    /// <returns>A string representing the generated ID.</returns>
    public string GenerateId()
    {
        return $"fake-id-{Count++}";
    }
}
