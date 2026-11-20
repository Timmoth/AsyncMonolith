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

        var consumer = scope.ServiceProvider.GetRequiredService(consumerType);
        var maxAttempts = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>().ResolveConsumerMaxAttempts(message);
        await ConsumerInvoker.Invoke(consumer, message, ConsumeContext.For(message, maxAttempts), null,
            cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the next message for the consumer <typeparamref name="TConsumer" />.
    /// </summary>
    /// <typeparam name="T">The type of DbContext.</typeparam>
    /// <typeparam name="TConsumer">The type of IConsumer.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed consumer message, or null if no message is available.</returns>
    public static async Task<ConsumerMessage?> ProcessNext<T, TConsumer>(IServiceScope scope,
        CancellationToken cancellationToken = default) where T : DbContext where TConsumer : IConsumer
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var consumerMessageSet = dbContext.Set<ConsumerMessage>();
        var consumerType = typeof(TConsumer);
        var message = await consumerMessageSet
            .Where(m => m.ConsumerType == AsyncMonolithNames.ForConsumer(consumerType))
            .OrderBy(m => m.AvailableAfter)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            return null;
        }

        var consumer = scope.ServiceProvider.GetRequiredService(consumerType);
        var maxAttempts = scope.ServiceProvider.GetRequiredService<ConsumerRegistry>().ResolveConsumerMaxAttempts(message);
        await ConsumerInvoker.Invoke(consumer, message, ConsumeContext.For(message, maxAttempts), null,
            cancellationToken);

        consumerMessageSet.Remove(message);

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        return message;
    }

    /// <summary>
    /// Processes the consumer message of type T and TPayload with the given payload.
    /// </summary>
    /// <typeparam name="T">The consumer type.</typeparam>
    /// <typeparam name="TPayload">The type of IConsumerPayload.</typeparam>
    /// <param name="scope">The service scope.</param>
    /// <param name="payload">The payload for the consumer message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task Process<T, TPayload>(IServiceScope scope, TPayload payload,
        CancellationToken cancellationToken = default) where T : IConsumer<TPayload> where TPayload : IConsumerPayload
    {
        var consumer = scope.ServiceProvider.GetRequiredService<T>();
        var context = new ConsumeContext
        {
            MessageId = "test-message",
            DeduplicationKey = "test-message",
            Attempt = 1,
            MaxAttempts = 1,
            CreatedAt = DateTimeOffset.UnixEpoch,
            ConsumerName = AsyncMonolithNames.ForConsumer<T>(),
            PayloadName = AsyncMonolithNames.ForPayload<TPayload>()
        };
        await consumer.Consume(payload, context, cancellationToken);
    }
}
