using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace Demo;

public class ValueSubmitted : IConsumerPayload
{
    [JsonPropertyName("value")] public required double Value { get; set; }
}