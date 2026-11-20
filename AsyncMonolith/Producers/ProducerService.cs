using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

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

    public ProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public async Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null,
        CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var payloadType = AsyncMonolithNames.ForPayload<TK>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);

        if (insertId is not null)
        {
            var pending = await PendingConsumers(insertId, consumers, cancellationToken);
            consumers = consumers.Where(c => !pending.Contains(c)).ToList();
        }

        Add(payloadType, JsonSerializer.Serialize(message), consumers, availableAfter,
            insertId ?? _idGenerator.GenerateId(), Activity.Current);
    }

    public Task ProduceList<TK>(List<TK> messages, long? availableAfter = null,
        CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var payloadType = AsyncMonolithNames.ForPayload<TK>();
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        foreach (var message in messages)
        {
            Add(payloadType, JsonSerializer.Serialize(message), consumers, availableAfter, _idGenerator.GenerateId(),
                Activity.Current);
        }

        return Task.CompletedTask;
    }

    public void Produce(ScheduledMessage message)
    {
        Add(message.PayloadType, message.Payload, _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType),
            null, _idGenerator.GenerateId(), null);
    }

    private async Task<HashSet<string>> PendingConsumers(string insertId, IReadOnlyList<string> consumers,
        CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ConsumerMessage>();
        var pending = set.Local
            .Where(m => m.InsertId == insertId && consumers.Contains(m.ConsumerType))
            .Select(m => m.ConsumerType)
            .ToHashSet();

        var saved = await set.AsNoTracking()
            .Where(m => m.InsertId == insertId && consumers.Contains(m.ConsumerType))
            .Select(m => m.ConsumerType)
            .ToListAsync(cancellationToken);
        pending.UnionWith(saved);
        return pending;
    }

    private void Add(string payloadType, string payload, IEnumerable<string> consumers, long? availableAfter,
        string insertId, Activity? activity)
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
                InsertId = insertId,
                TraceId = activity?.TraceId.ToString(),
                SpanId = activity?.SpanId.ToString()
            });
        }
    }
}
