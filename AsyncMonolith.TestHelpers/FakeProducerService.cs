using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Represents a fake implementation of the <see cref="IProducerService"/> interface for testing purposes.
/// </summary>
public class FakeProducerService : IProducerService
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeProducerService"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="consumerRegistry">The consumer registry.</param>
    /// <param name="idGenerator">The ID generator.</param>
    public FakeProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _consumerRegistry = consumerRegistry ?? throw new ArgumentNullException(nameof(consumerRegistry));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    /// <summary>
    /// Gets or sets the list of consumer messages created by the producer service.
    /// </summary>
    public List<ConsumerMessage> CreatedConsumerMessages { get; set; } = new List<ConsumerMessage>();

    /// <summary>
    /// Produces a single consumer message.
    /// </summary>
    /// <typeparam name="TK">The type of the consumer payload.</typeparam>
    /// <param name="message">The consumer payload message.</param>
    /// <param name="availableAfter">The available after timestamp (in Unix time) for the consumer message.</param>
    /// <param name="insertId">The insert ID for the consumer message.</param>
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

        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(payloadType))
        {
            CreatedConsumerMessages.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = availableAfter.Value,
                ConsumerType = consumerId,
                PayloadType = payloadType,
                Payload = payload,
                Attempts = 0,
                InsertId = insertId
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces a list of consumer messages.
    /// </summary>
    /// <typeparam name="TK">The type of the consumer payload.</typeparam>
    /// <param name="messages">The list of consumer payload messages.</param>
    /// <param name="availableAfter">The available after timestamp (in Unix time) for the consumer messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ProduceList<TK>(List<TK> messages, long? availableAfter = null,
        CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var payloadType = typeof(TK).Name;
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        foreach (var message in messages)
        {
            var insertId = _idGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);

            foreach (var consumerId in consumers)
            {
                CreatedConsumerMessages.Add(new ConsumerMessage
                {
                    Id = _idGenerator.GenerateId(),
                    CreatedAt = currentTime,
                    AvailableAfter = availableAfter.Value,
                    ConsumerType = consumerId,
                    PayloadType = payloadType,
                    Payload = payload,
                    Attempts = 0,
                    InsertId = insertId
                });
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces a scheduled consumer message.
    /// </summary>
    /// <param name="message">The scheduled consumer message.</param>
    public void Produce(ScheduledMessage message)
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var insertId = _idGenerator.GenerateId();
        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType))
        {
            CreatedConsumerMessages.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
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
}
