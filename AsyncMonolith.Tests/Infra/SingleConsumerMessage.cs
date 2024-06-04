using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class SingleConsumerMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}