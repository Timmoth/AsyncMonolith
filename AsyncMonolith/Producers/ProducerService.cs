using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Producers;

/// <summary>
/// Base class for producer services.
/// </summary>
/// <typeparam name="T">The type of the DbContext.</typeparam>
public abstract class ProducerService<T> where T : DbContext
{
    protected readonly ConsumerRegistry ConsumerRegistry;
    protected readonly T DbContext;
    protected readonly IAsyncMonolithIdGenerator IdGenerator;
    protected readonly TimeProvider TimeProvider;

    protected ProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        TimeProvider = timeProvider;
        ConsumerRegistry = consumerRegistry;
        DbContext = dbContext;
        IdGenerator = idGenerator;
    }

    /// <summary>
    /// Produces a single message
    /// </summary>
    /// <typeparam name="TK">The type of the message.</typeparam>
    /// <param name="message">The message to produce.</param>
    /// <param name="availableAfter">The time in seconds after which the message should be available for consumption.</param>
    /// <param name="insertId">The insert ID for the message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null)
        where TK : IConsumerPayload;

    /// <summary>
    /// Produces a list of messages of type TK.
    /// </summary>
    /// <typeparam name="TK">The type of the messages.</typeparam>
    /// <param name="messages">The list of messages to produce.</param>
    /// <param name="availableAfter">The time in seconds after which the messages should be available for consumption.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public abstract Task ProduceList<TK>(List<TK> messages, long? availableAfter = null) where TK : IConsumerPayload;

    /// <summary>
    /// Produces a scheduled message.
    /// </summary>
    /// <param name="message">The scheduled message to produce.</param>
    public void Produce(ScheduledMessage message)
    {
        var currentTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = DbContext.Set<ConsumerMessage>();
        var insertId = IdGenerator.GenerateId();
        foreach (var consumerId in ConsumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType))
            set.Add(new ConsumerMessage
            {
                Id = IdGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = currentTime,
                ConsumerType = consumerId,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Attempts = 0,
                InsertId = insertId
            });
    }
}
