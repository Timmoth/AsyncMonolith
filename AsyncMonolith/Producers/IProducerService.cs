using AsyncMonolith.Consumers;

namespace AsyncMonolith.Producers;

/// <summary>
///     Produces messages for consumers. Messages are added to your DbContext and written when you call
///     SaveChanges, in the same transaction as your other changes.
/// </summary>
public interface IProducerService
{
    /// <summary>
    ///     Produces a message for every consumer of <typeparamref name="TPayload" />.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="delay">How long to wait before the message is consumed. Null means straight away.</param>
    /// <param name="deduplicationKey">
    ///     Skips consumers that already have a pending message with this key. Leave null to always produce.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Produce<TPayload>(TPayload payload, TimeSpan? delay = null, string? deduplicationKey = null,
        CancellationToken cancellationToken = default)
        where TPayload : IConsumerPayload;

    /// <summary>
    ///     Produces a message for every consumer of <typeparamref name="TPayload" />, to be consumed at or after
    ///     <paramref name="availableAt" />.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="availableAt">The earliest time the message is consumed.</param>
    /// <param name="deduplicationKey">
    ///     Skips consumers that already have a pending message with this key. Leave null to always produce.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Produce<TPayload>(TPayload payload, DateTimeOffset availableAt, string? deduplicationKey = null,
        CancellationToken cancellationToken = default)
        where TPayload : IConsumerPayload;

    /// <summary>
    ///     Produces a message per payload for every consumer of <typeparamref name="TPayload" />.
    /// </summary>
    /// <param name="payloads">The payloads.</param>
    /// <param name="delay">How long to wait before the messages are consumed. Null means straight away.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ProduceList<TPayload>(IEnumerable<TPayload> payloads, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
        where TPayload : IConsumerPayload;
}
