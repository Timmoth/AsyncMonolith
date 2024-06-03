using AsnyMonolith.Producers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsnyMonolith.Scheduling;

public sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ILogger<ScheduledMessageProcessor<T>> _logger;
    private readonly IServiceProvider _services;

    private readonly TimeProvider _timeProvider;

    public ScheduledMessageProcessor(ILogger<ScheduledMessageProcessor<T>> logger, IServiceProvider services,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _services = services;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scheduledMessageChainLength = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ConsumeNext(stoppingToken))
                    scheduledMessageChainLength++;
                else
                    scheduledMessageChainLength = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling next message");
            }

            await Task.Delay(scheduledMessageChainLength switch
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
        var producer = scope.ServiceProvider.GetRequiredService<ProducerService<T>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = dbContext.Set<ScheduledMessage>();

        var message = await set
            .Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null) return false;

        message.AvailableAfter = currentTime + message.Delay;

        try
        {
            producer.Produce(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule message");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}