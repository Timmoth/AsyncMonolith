using System.Text.Json;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Base class for consumers.
/// </summary>
/// <typeparam name="T">The type of the consumer payload.</typeparam>
public abstract class BaseConsumer<T> : IConsumer where T : IConsumerPayload
{
    /// <summary>
    ///     Internal method called by the processor to deserialize and process consumer payloads.
    /// </summary>
    /// <param name="message">The consumer message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Consume(ConsumerMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<T>(message.Payload) ?? throw new Exception(
            $"Consumer: '{message.ConsumerType}' failed to deserialize payload: '{message.PayloadType}'");

        await Consume(payload!, cancellationToken);
    }

    /// <summary>
    ///     Consumes the payload.
    /// </summary>
    /// <param name="payload">The consumer payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task Consume(T payload, CancellationToken cancellationToken = default);
}