namespace AsyncMonolith.Consumers;

/// <summary>
///     Marks a type as a consumer. Implement <see cref="IConsumer{TPayload}" /> rather than this interface.
/// </summary>
public interface IConsumer;

/// <summary>
///     Consumes messages with a <typeparamref name="TPayload" /> payload. Implementations are found by
///     assembly scanning, registered as scoped services, and run once for each message.
/// </summary>
/// <remarks>
///     A message may be delivered more than once (for example if the app stops mid-message), so make consumers
///     idempotent. <see cref="ConsumeContext.MessageId" /> and <see cref="ConsumeContext.DeduplicationKey" /> help.
/// </remarks>
/// <typeparam name="TPayload">The payload type this consumer handles.</typeparam>
public interface IConsumer<in TPayload> : IConsumer where TPayload : IConsumerPayload
{
    /// <summary>
    ///     Consumes one message. Throwing, or not finishing within the consumer's timeout, counts as a failed
    ///     attempt; the message is retried until its attempts run out, then poisoned.
    /// </summary>
    /// <param name="payload">The message payload.</param>
    /// <param name="context">Details of the message and this attempt.</param>
    /// <param name="cancellationToken">Cancelled when the consumer times out or the app is stopping.</param>
    Task Consume(TPayload payload, ConsumeContext context, CancellationToken cancellationToken);
}
