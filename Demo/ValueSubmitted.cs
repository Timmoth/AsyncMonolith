using System.Text.Json.Serialization;
using AsnyMonolith.Consumers;

namespace Demo;

public class ValueSubmitted : IConsumerPayload
{
    [JsonPropertyName("value")] public required double Value { get; set; }
}