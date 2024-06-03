using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsnyMonolith.Consumers;

public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IServiceProvider _services;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<AsyncMonolithSettings> _options;

    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger, IServiceProvider services,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry, IOptions<AsyncMonolithSettings> options)
    {
        _logger = logger;
        _services = services;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumedMessageChainLength = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ConsumeNext(stoppingToken))
                    consumedMessageChainLength++;
                else
                    consumedMessageChainLength = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message");
            }

            await Task.Delay(consumedMessageChainLength switch
            {
                <= 1 => 1000,
                <= 2 => 500,
                <= 5 => 250,
                <= 10 => 100,
                _ => 50
            }, stoppingToken);
        }
    }

    public async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var consumerSet = dbContext.Set<ConsumerMessage>();

        var message = await consumerSet
            .Where(m => m.AvailableAfter <= currentTime && m.Attempts <= _options.Value.MaxAttempts)
            .OrderBy(m => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
            // No messages waiting.
            return false;

        try
        {
            message.AvailableAfter = currentTime + _options.Value.AttemptDelay;
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
                message.Attempts > _options.Value.MaxAttempts
                    ? "Failed to consume message on attempt {attempt}, will NOT retry"
                    : "Failed to consume message on attempt {attempt}, will retry", message.Attempts);
        }

        return true;
    }
}