using System.Text.Json;
using AsnyMonolith.Consumers;
using AsnyMonolith.Scheduling;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsnyMonolith.Producers;

public sealed class ProducerService<T> where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;

    private readonly T _dbContext;
    private readonly IAsnyMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsnyMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public void Produce<TK>(TK message, long? availableAfter = null) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);

        var payloadType = typeof(TK).Name;
        var set = _dbContext.Set<ConsumerMessage>();

        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(payloadType))
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = availableAfter.Value,
                ConsumerType = consumerId,
                PayloadType = payloadType,
                Payload = payload,
                Attempts = 0
            });
    }

    public void Produce(ScheduledMessage message)
    {
        var consumers = _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType);
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = _dbContext.Set<ConsumerMessage>();
        foreach (var consumerId in consumers)
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = currentTime,
                ConsumerType = consumerId,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Attempts = 0
            });
    }
}