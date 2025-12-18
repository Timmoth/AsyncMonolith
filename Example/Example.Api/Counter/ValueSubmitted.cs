using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace Example.Api.Counter;

public class ValueSubmitted : IConsumerPayload
{
    [JsonPropertyName("value")] public required double Value { get; set; }
}