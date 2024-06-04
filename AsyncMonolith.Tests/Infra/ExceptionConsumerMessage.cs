using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class ExceptionConsumerMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}