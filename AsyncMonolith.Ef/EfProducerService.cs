using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Ef;

public class EfProducerService<T> : ProducerService<T> where T : DbContext
{
    public EfProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator) : base(timeProvider, consumerRegistry, dbContext, idGenerator)
    {
    }

    public override Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null)
    {
        var currentTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);
        insertId ??= IdGenerator.GenerateId();
        var payloadType = typeof(TK).Name;
        var set = DbContext.Set<ConsumerMessage>();

        foreach (var consumerId in ConsumerRegistry.ResolvePayloadConsumerTypes(payloadType))
            set.Add(new ConsumerMessage
            {
                Id = IdGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = availableAfter.Value,
                ConsumerType = consumerId,
                PayloadType = payloadType,
                Payload = payload,
                Attempts = 0,
                InsertId = insertId
            });

        return Task.CompletedTask;
    }

    public override Task ProduceList<TK>(List<TK> messages, long? availableAfter = null)
    {
        var currentTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var set = DbContext.Set<ConsumerMessage>();

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var insertId = IdGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);
            var payloadType = typeof(TK).Name;

            foreach (var consumerId in ConsumerRegistry.ResolvePayloadConsumerTypes(payloadType))
                set.Add(new ConsumerMessage
                {
                    Id = IdGenerator.GenerateId(),
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
}