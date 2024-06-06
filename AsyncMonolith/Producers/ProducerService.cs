using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Producers;

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

    public abstract Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null)
        where TK : IConsumerPayload;

    public abstract Task ProduceList<TK>(List<TK> messages, long? availableAfter = null) where TK : IConsumerPayload;

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