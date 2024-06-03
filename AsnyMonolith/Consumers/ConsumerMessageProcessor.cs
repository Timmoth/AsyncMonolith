using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsnyMonolith.Consumers;

public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IServiceProvider _services;
    private readonly TimeProvider _timeProvider;

    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger, IServiceProvider services,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry)
    {
        _logger = logger;
        _services = services;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var successfullyConsumed = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ConsumeNext(stoppingToken))
                    successfullyConsumed++;
                else
                    successfullyConsumed = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming next message");
            }

            var delay = successfullyConsumed switch
            {
                <= 1 => 1000,
                <= 2 => 500,
                <= 5 => 250,
                <= 10 => 100,
                _ => 50
            };
            await Task.Delay(delay, stoppingToken);
        }
    }

    public async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var consumerSet = dbContext.Set<ConsumerMessage>();

        var message = await consumerSet
            .Where(m => m.AvailableAfter <= currentTime && m.Attempts <= 5)
            .OrderBy(m => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
            // No messages waiting.
            return false;

        try
        {
            message.AvailableAfter = currentTime + 10;
            message.Attempts++;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // ignore
            return false;
        }

        try
        {
            if (scope.ServiceProvider.GetRequiredService(_consumerRegistry.ResolveConsumerType(message))
                is not IConsumer consumer)
                throw new Exception($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");

            await consumer.Consume(message, cancellationToken);

            consumerSet.Remove(message);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully processed message for consumer: '{id}'", message.ConsumerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                message.Attempts > 5
                    ? "Failed to consume message on attempt {attempt}, moving to failed messages"
                    : "Failed to consume message on attempt {attempt}, will retry", message.Attempts);
        }

        return true;
    }
}