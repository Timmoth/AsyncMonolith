using System.Text.Json;
using AsyncMonolith.Consumers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Helper class for testing consumer message processing.
/// </summary>
public static class TestConsumerMessageProcessor
{
    /// <summary>
    /// Processes the next consumer message of type T.
    /// </summary>
    /// <typeparam name="T">The type of DbContext.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed consumer message, or null if no message is available.</returns>
    public static async Task<ConsumerMessage?> ProcessNext<T>(IServiceScope scope,
        CancellationToken cancellationToken = default) where T : DbContext
    {
        var consumerRegistry = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var consumerMessageSet = dbContext.Set<ConsumerMessage>();
        var message = await consumerMessageSet
            .OrderBy(m => m.AvailableAfter)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return null;
        }

        var consumerType = consumerRegistry.ResolveConsumerType(message);

        // Resolve the consumer
        if (scope.ServiceProvider.GetRequiredService(consumerType)
            is not IConsumer consumer)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            return null;
        }

        // Execute the consumer
        await consumer.Consume(message, cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the next consumer message of type T and V.
    /// </summary>
    /// <typeparam name="T">The type of DbContext.</typeparam>
    /// <typeparam name="V">The type of IConsumer.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed consumer message, or null if no message is available.</returns>
    public static async Task<ConsumerMessage?> ProcessNext<T, V>(IServiceScope scope,
        CancellationToken cancellationToken = default) where T : DbContext where V : IConsumer
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var consumerMessageSet = dbContext.Set<ConsumerMessage>();
        var consumerType = typeof(T);
        var message = await consumerMessageSet
            .Where(m => m.ConsumerType == consumerType.Name)
            .OrderBy(m => m.AvailableAfter)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return null;
        }

        // Resolve the consumer
        if (scope.ServiceProvider.GetRequiredService(consumerType)
            is not IConsumer consumer)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            return null;
        }

        // Execute the consumer
        await consumer.Consume(message, cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the consumer message of type T and V with the given payload.
    /// </summary>
    /// <typeparam name="T">The type of BaseConsumer.</typeparam>
    /// <typeparam name="V">The type of IConsumerPayload.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="payload">The payload for the consumer message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Process<T, V>(IServiceScope scope, V payload,
        CancellationToken cancellationToken = default) where T : BaseConsumer<V> where V : IConsumerPayload
    {
        var consumerType = typeof(T);

        // Resolve the consumer
        if (scope.ServiceProvider.GetRequiredService(consumerType)
            is not IConsumer consumer)
        {
            Assert.Fail($"Couldn't resolve consumer service of type: '{consumerType.Name}'");
            return;
        }

        // Execute the consumer
        await consumer.Consume(new ConsumerMessage
        {
            Attempts = 0,
            AvailableAfter = 0,
            ConsumerType = consumerType.Name,
            CreatedAt = 0,
            Id = string.Empty,
            InsertId = string.Empty,
            Payload = JsonSerializer.Serialize(payload),
            PayloadType = typeof(V).Name
        }, cancellationToken);
    }
}
