using System.Text.Json.Serialization;
using AsnyMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class SingleConsumerMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}