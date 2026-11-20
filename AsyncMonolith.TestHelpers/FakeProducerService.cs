using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;

namespace AsyncMonolith.TestHelpers;

/// <summary>
///     An <see cref="IProducerService" /> that records produced messages in <see cref="CreatedConsumerMessages" />
///     instead of writing them to a database.
/// </summary>
public class FakeProducerService : IProducerService
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="FakeProducerService" /> class.</summary>
    public FakeProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _consumerRegistry = consumerRegistry ?? throw new ArgumentNullException(nameof(consumerRegistry));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    /// <summary>The messages produced so far, one per consumer.</summary>
    public List<ConsumerMessage> CreatedConsumerMessages { get; } = [];

    /// <inheritdoc />
    public Task Produce<TPayload>(TPayload payload, TimeSpan? delay = null, string? deduplicationKey = null,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload =>
        Produce(payload, _timeProvider.GetUtcNow() + (delay ?? TimeSpan.Zero), deduplicationKey, cancellationToken);

    /// <inheritdoc />
    public Task Produce<TPayload>(TPayload payload, DateTimeOffset availableAt, string? deduplicationKey = null,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload
    {
        var payloadType = AsyncMonolithNames.ForPayload<TPayload>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType)
            .Where(c => deduplicationKey is null || !CreatedConsumerMessages.Any(m =>
                m.DeduplicationKey == deduplicationKey && m.ConsumerType == c));
        Record(payloadType, JsonSerializer.Serialize(payload), consumers, availableAt,
            deduplicationKey ?? _idGenerator.GenerateId());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ProduceList<TPayload>(IEnumerable<TPayload> payloads, TimeSpan? delay = null,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload
    {
        var payloadType = AsyncMonolithNames.ForPayload<TPayload>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        var availableAt = _timeProvider.GetUtcNow() + (delay ?? TimeSpan.Zero);
        foreach (var payload in payloads)
        {
            Record(payloadType, JsonSerializer.Serialize(payload), consumers, availableAt, _idGenerator.GenerateId());
        }

        return Task.CompletedTask;
    }

    private void Record(string payloadType, string payload, IEnumerable<string> consumers, DateTimeOffset availableAt,
        string deduplicationKey)
    {
        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        foreach (var consumer in consumers)
        {
            CreatedConsumerMessages.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = now,
                AvailableAfter = Math.Max(now, availableAt.ToUnixTimeSeconds()),
                ConsumerType = consumer,
                PayloadType = payloadType,
                Payload = payload,
                Attempts = 0,
                DeduplicationKey = deduplicationKey,
                TraceId = null,
                SpanId = null,
                RailId = _consumerRegistry.ResolveConsumerRailId(consumer)
            });
        }
    }
}
