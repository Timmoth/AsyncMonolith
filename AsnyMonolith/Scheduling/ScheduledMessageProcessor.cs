using AsnyMonolith.Producers;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsnyMonolith.Scheduling;

public sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private const int MaxChainLength = 10;
    private readonly ILogger<ScheduledMessageProcessor<T>> _logger;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly TimeProvider _timeProvider;

    public ScheduledMessageProcessor(ILogger<ScheduledMessageProcessor<T>> logger,
        TimeProvider timeProvider, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scheduledMessageChainLength = 0;
        var deltaDelay = (_options.Value.ProcessorMaxDelay - _options.Value.ProcessorMinDelay) / MaxChainLength;
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

            var delay = _options.Value.ProcessorMaxDelay -
                        deltaDelay * Math.Clamp(scheduledMessageChainLength, 0, MaxChainLength);
            if (delay >= 10) await Task.Delay(delay, stoppingToken);
        }
    }

    public async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var producer = scope.ServiceProvider.GetRequiredService<ProducerService<T>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = dbContext.Set<ScheduledMessage>();

        var message = await set
            .Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => Guid.NewGuid())
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null) return false;

        message.AvailableAfter = message.GetNextOccurrence(_timeProvider);

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