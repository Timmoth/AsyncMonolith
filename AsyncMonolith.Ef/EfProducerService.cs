using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Ef;

/// <summary>
/// Represents a service for producing messages using Entity Framework.
/// </summary>
/// <typeparam name="T">The type of the DbContext.</typeparam>
public sealed class EfProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfProducerService{T}"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="consumerRegistry">The consumer registry.</param>
    /// <param name="dbContext">The DbContext.</param>
    /// <param name="idGenerator">The ID generator.</param>
    public EfProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// Produces a single message.
    /// </summary>
    /// <typeparam name="TK">The type of the message.</typeparam>
    /// <param name="message">The message to produce.</param>
    /// <param name="availableAfter">The time when the message should be available for consumption.</param>
    /// <param name="insertId">The ID to insert for the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null,
        CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);
        insertId ??= _idGenerator.GenerateId();
        var payloadType = typeof(TK).Name;
        var set = _dbContext.Set<ConsumerMessage>();
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(payloadType))
        {
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = availableAfter.Value,
                ConsumerType = consumerId,
                PayloadType = payloadType,
                Payload = payload,
                Attempts = 0,
                InsertId = insertId,
                TraceId = traceId,
                SpanId = spanId
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces a list of messages.
    /// </summary>
    /// <typeparam name="TK">The type of the messages.</typeparam>
    /// <param name="messages">The messages to produce.</param>
    /// <param name="availableAfter">The time when the messages should be available for consumption.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ProduceList<TK>(List<TK> messages, long? availableAfter = null,
        CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var set = _dbContext.Set<ConsumerMessage>();
        var payloadType = typeof(TK).Name;
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        foreach (var message in messages)
        {
            var insertId = _idGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);

            foreach (var consumerId in consumers)
            {
                set.Add(new ConsumerMessage
                {
                    Id = _idGenerator.GenerateId(),
                    CreatedAt = currentTime,
                    AvailableAfter = availableAfter.Value,
                    ConsumerType = consumerId,
                    PayloadType = payloadType,
                    Payload = payload,
                    Attempts = 0,
                    InsertId = insertId,
                    TraceId = traceId,
                    SpanId = spanId
                });
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces a scheduled message.
    /// </summary>
    /// <param name="message">The scheduled message to produce.</param>
    public void Produce(ScheduledMessage message)
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = _dbContext.Set<ConsumerMessage>();
        var insertId = _idGenerator.GenerateId();
        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType))
        {
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = currentTime,
                ConsumerType = consumerId,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Attempts = 0,
                InsertId = insertId,
                TraceId = null,
                SpanId = null
            });
        }
    }
}
