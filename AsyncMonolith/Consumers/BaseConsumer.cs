using System.Text.Json;

namespace AsyncMonolith.Consumers;

public abstract class BaseConsumer<T> : IConsumer where T : IConsumerPayload
{
    public async Task Consume(ConsumerMessage message, CancellationToken token)
    {
        var payload = JsonSerializer.Deserialize<T>(message.Payload);
        if (payload == null)
            throw new Exception(
                $"Consumer: '{message.ConsumerType}' failed to deserialize payload: '{message.PayloadType}'");

        await Consume(payload!, token);
    }

    public abstract Task Consume(T payload, CancellationToken token);
}