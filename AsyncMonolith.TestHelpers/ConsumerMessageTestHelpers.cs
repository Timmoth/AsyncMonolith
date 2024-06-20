using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Helper methods for testing consumer messages.
/// </summary>
public static class ConsumerMessageTestHelpers
{
    /// <summary>
    /// Retrieves a list of consumer messages based on the specified payload type.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <returns>A list of consumer messages.</returns>
    public static async Task<List<ConsumerMessage>> GetConsumerMessages<T>(this DbContext dbContext)
        where T : IConsumerPayload
    {
        var payloadType = typeof(T);
        return await dbContext.Set<ConsumerMessage>()
            .Where(m =>
                m.PayloadType == payloadType.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a list of consumer messages based on the specified consumer type and payload type.
    /// </summary>
    /// <typeparam name="T">The type of the consumer.</typeparam>
    /// <typeparam name="V">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <returns>A list of consumer messages.</returns>
    public static async Task<List<ConsumerMessage>> GetConsumerMessages<T, V>(this DbContext dbContext)
        where T : BaseConsumer<V> where V : IConsumerPayload
    {
        var consumerType = typeof(T);
        var payloadType = typeof(V);
        return await dbContext.Set<ConsumerMessage>()
            .Where(m =>
                m.ConsumerType == consumerType.Name &&
                m.PayloadType == payloadType.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a list of consumer messages based on the specified consumer type, payload type, and payload.
    /// </summary>
    /// <typeparam name="T">The type of the consumer.</typeparam>
    /// <typeparam name="V">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>A list of consumer messages.</returns>
    public static async Task<List<ConsumerMessage>> GetConsumerMessages<T, V>(this DbContext dbContext, V payload)
        where T : BaseConsumer<V> where V : IConsumerPayload
    {
        var consumerType = typeof(T);
        var payloadType = typeof(V);
        var serializedPayload = JsonSerializer.Serialize(payload);
        return await dbContext.Set<ConsumerMessage>()
            .Where(m =>
                m.ConsumerType == consumerType.Name &&
                m.PayloadType == payloadType.Name &&
                m.Payload == serializedPayload)
            .ToListAsync();
    }

    /// <summary>
    /// Asserts that there is a single consumer message based on the specified consumer type, payload type, and payload.
    /// </summary>
    /// <typeparam name="T">The type of the consumer.</typeparam>
    /// <typeparam name="V">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The single consumer message.</returns>
    public static async Task<ConsumerMessage> AssertSingleConsumerMessage<T, V>(this DbContext dbContext, V payload)
        where T : BaseConsumer<V> where V : IConsumerPayload
    {
        var consumerType = typeof(T);
        var payloadType = typeof(V);
        var serializedPayload = JsonSerializer.Serialize(payload);
        var messages = await dbContext.Set<ConsumerMessage>()
            .Where(m =>
                m.ConsumerType == consumerType.Name &&
                m.PayloadType == payloadType.Name &&
                m.Payload == serializedPayload)
            .ToListAsync();

        return Assert.Single(messages);
    }

    /// <summary>
    /// Asserts that there is a single scheduled message based on the specified payload type and payload.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The single scheduled message.</returns>
    public static async Task<ScheduledMessage> AssertSingleScheduledMessage<T>(this DbContext dbContext, T payload)
        where T : IConsumerPayload
    {
        var payloadType = typeof(T);
        var serializedPayload = JsonSerializer.Serialize(payload);
        var messages = await dbContext.Set<ScheduledMessage>()
            .Where(m =>
                m.PayloadType == payloadType.Name &&
                m.Payload == serializedPayload)
            .ToListAsync();

        return Assert.Single(messages);
    }

    /// <summary>
    /// Asserts that there is a single consumer message based on the specified consumer type, payload type, payload, and ID.
    /// </summary>
    /// <typeparam name="T">The type of the consumer.</typeparam>
    /// <typeparam name="V">The type of the payload.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="id">The ID.</param>
    /// <returns>The single consumer message.</returns>
    public static async Task<ConsumerMessage> AssertSingleConsumerMessageById<T, V>(this DbContext dbContext, V payload,
        string id) where T : BaseConsumer<V> where V : IConsumerPayload
    {
        var consumerType = typeof(T);
        var payloadType = typeof(V);
        var serializedPayload = JsonSerializer.Serialize(payload);
        var messages = await dbContext.Set<ConsumerMessage>()
            .Where(m =>
                m.ConsumerType == consumerType.Name &&
                m.PayloadType == payloadType.Name &&
                m.Payload == serializedPayload &&
                m.Id == id)
            .ToListAsync();

        return Assert.Single(messages);
    }
}
