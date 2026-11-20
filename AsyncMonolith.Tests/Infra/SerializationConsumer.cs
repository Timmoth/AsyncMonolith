using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public enum Priority
{
    Normal,
    Urgent
}

/// <summary>No [JsonPropertyName] attributes, so the serializer options decide the stored names.</summary>
public class SerializationMessage : IConsumerPayload
{
    public required string FirstName { get; set; }
    public Priority Priority { get; set; }
}

public sealed class SerializationRecorder
{
    public List<SerializationMessage> Received { get; } = [];
}

public class SerializationConsumer(SerializationRecorder recorder) : IConsumer<SerializationMessage>
{
    public Task Consume(SerializationMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        recorder.Received.Add(payload);
        return Task.CompletedTask;
    }
}
