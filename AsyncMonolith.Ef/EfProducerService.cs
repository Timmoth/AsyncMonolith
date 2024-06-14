using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Ef;

public sealed class EfProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public EfProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null, CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);
        insertId ??= _idGenerator.GenerateId();
        var payloadType = typeof(TK).Name;
        var set = _dbContext.Set<ConsumerMessage>();

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
                InsertId = insertId
            });
        }

        return Task.CompletedTask;
    }

    public Task ProduceList<TK>(List<TK> messages, long? availableAfter = null, CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var set = _dbContext.Set<ConsumerMessage>();
        var payloadType = typeof(TK).Name;
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
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
                    InsertId = insertId
                });
            }
        }

        return Task.CompletedTask;
    }

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
                InsertId = insertId
            });
        }
    }
}