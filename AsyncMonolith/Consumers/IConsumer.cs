namespace AsyncMonolith.Consumers;

/// <summary>
/// Interface for Consumers
/// </summary>
public interface IConsumer
{
    /// <summary>
    /// Consumes the given message.
    /// </summary>
    /// <param name="message">The consumer message to be consumed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Consume(ConsumerMessage message, CancellationToken cancellationToken);
}
