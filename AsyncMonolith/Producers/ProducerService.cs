using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Producers;

/// <summary>
///     The producer for every provider. Messages are added to the caller's DbContext and written when the caller
///     calls SaveChanges, in the same transaction as their other changes (a transactional outbox).
/// </summary>
/// <remarks>
///     When an insert id is given, consumers that already have a pending message with that insert id are skipped,
///     whether it is saved or only added to this DbContext. Two producers racing on the same insert id in separate
///     transactions are still stopped by the unique index, and the second SaveChanges throws.
/// </remarks>
/// <typeparam name="T">The DbContext containing the AsyncMonolith tables.</typeparam>
internal sealed class ProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly JsonSerializerOptions? _serializerOptions;

    public ProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator, IOptions<AsyncMonolithOptions> options)
    {
        _serializerOptions = options.Value.SerializerOptions;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public Task Produce<TPayload>(TPayload payload, TimeSpan? delay = null, string? deduplicationKey = null,
        CancellationToken cancellationToken = default)
        where TPayload : IConsumerPayload =>
        Produce(payload, AvailableAfter(delay), deduplicationKey, cancellationToken);

    public Task Produce<TPayload>(TPayload payload, DateTimeOffset availableAt, string? deduplicationKey = null,
        CancellationToken cancellationToken = default)
        where TPayload : IConsumerPayload =>
        Produce(payload, ToStoredTime(availableAt), deduplicationKey, cancellationToken);

    public Task ProduceList<TPayload>(IEnumerable<TPayload> payloads, TimeSpan? delay = null,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload
    {
        ArgumentNullException.ThrowIfNull(payloads);
        var payloadType = AsyncMonolithNames.ForPayload<TPayload>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        var availableAfter = AvailableAfter(delay);
        foreach (var payload in payloads)
        {
            Add(payloadType, JsonSerializer.Serialize(payload, _serializerOptions), consumers, availableAfter, _idGenerator.GenerateId(),
                Activity.Current);
        }

        return Task.CompletedTask;
    }

    /// <summary>Produces the message for a due schedule. Used by the scheduled message processor.</summary>
    internal void ProduceScheduled(ScheduledMessage message)
    {
        Add(message.PayloadType, message.Payload, _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType),
            null, _idGenerator.GenerateId(), null);
    }

    private async Task Produce<TPayload>(TPayload payload, long availableAfter, string? deduplicationKey,
        CancellationToken cancellationToken) where TPayload : IConsumerPayload
    {
        var payloadType = AsyncMonolithNames.ForPayload<TPayload>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);

        if (deduplicationKey is not null)
        {
            var pending = await PendingConsumers(deduplicationKey, consumers, cancellationToken);
            consumers = consumers.Where(c => !pending.Contains(c)).ToList();
        }

        Add(payloadType, JsonSerializer.Serialize(payload, _serializerOptions), consumers, availableAfter,
            deduplicationKey ?? _idGenerator.GenerateId(), Activity.Current);
    }

    private long AvailableAfter(TimeSpan? delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The delay must not be negative.");
        }

        return ToStoredTime(_timeProvider.GetUtcNow() + (delay ?? TimeSpan.Zero));
    }

    // Stored times are whole seconds. A future time is rounded up, so a message is never consumed before it was
    // asked to be; a time that has already passed is stored as now, so the message is available straight away.
    private long ToStoredTime(DateTimeOffset time)
    {
        var now = _timeProvider.GetUtcNow();
        return time <= now ? now.ToUnixTimeSeconds() : (time.ToUnixTimeMilliseconds() + 999) / 1000;
    }

    private async Task<HashSet<string>> PendingConsumers(string deduplicationKey, IReadOnlyList<string> consumers,
        CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ConsumerMessage>();
        var pending = set.Local
            .Where(m => m.DeduplicationKey == deduplicationKey && consumers.Contains(m.ConsumerType))
            .Select(m => m.ConsumerType)
            .ToHashSet();

        var saved = await set.AsNoTracking()
            .Where(m => m.DeduplicationKey == deduplicationKey && consumers.Contains(m.ConsumerType))
            .Select(m => m.ConsumerType)
            .ToListAsync(cancellationToken);
        pending.UnionWith(saved);
        return pending;
    }

    private void Add(string payloadType, string payload, IEnumerable<string> consumers, long? availableAfter,
        string deduplicationKey, Activity? activity)
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = _dbContext.Set<ConsumerMessage>();
        foreach (var consumer in consumers)
        {
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = availableAfter ?? currentTime,
                Attempts = 0,
                ConsumerType = consumer,
                RailId = _consumerRegistry.ResolveConsumerRailId(consumer),
                PayloadType = payloadType,
                Payload = payload,
                DeduplicationKey = deduplicationKey,
                TraceId = activity?.TraceId.ToString(),
                SpanId = activity?.SpanId.ToString()
            });
        }
    }
}
