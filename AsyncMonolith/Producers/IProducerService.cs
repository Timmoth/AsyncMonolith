using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;

namespace AsyncMonolith.Producers;

/// <summary>
///    Interface for producing messages.
/// </summary>
public interface IProducerService
{
    /// <summary>
    ///     Produces a single message
    /// </summary>
    /// <typeparam name="TK">The type of the message.</typeparam>
    /// <param name="message">The message to produce.</param>
    /// <param name="availableAfter">The time in seconds after which the message should be available for consumption.</param>
    /// <param name="insertId">The insert ID for the message.</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null, CancellationToken cancellationToken = default)
        where TK : IConsumerPayload;

    /// <summary>
    ///     Produces a list of messages of type TK.
    /// </summary>
    /// <typeparam name="TK">The type of the messages.</typeparam>
    /// <param name="messages">The list of messages to produce.</param>
    /// <param name="availableAfter">The time in seconds after which the messages should be available for consumption.</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ProduceList<TK>(List<TK> messages, long? availableAfter = null, CancellationToken cancellationToken = default) where TK : IConsumerPayload;

    /// <summary>
    ///     Produces a scheduled message.
    /// </summary>
    /// <param name="message">The scheduled message to produce.</param>
    public void Produce(ScheduledMessage message);
}