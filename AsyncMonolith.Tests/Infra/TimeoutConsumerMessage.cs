using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class TimeoutConsumerMessage : IConsumerPayload
{
    [JsonPropertyName("delay")] public required int Delay { get; set; }
}