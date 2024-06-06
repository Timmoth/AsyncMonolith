using System.Diagnostics;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Scheduling;

public sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ILogger<ScheduledMessageProcessor<T>> _logger;
    private readonly ScheduledMessageFetcher _messageFetcher;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public ScheduledMessageProcessor(ILogger<ScheduledMessageProcessor<T>> logger,
        TimeProvider timeProvider, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory,
        ScheduledMessageFetcher messageFetcher)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options;
        _scopeFactory = scopeFactory;
        _messageFetcher = messageFetcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _options.Value.ProcessorMaxDelay;
            try
            {
                var processedScheduledMessages = await ProcessBatch(stoppingToken);

                if (processedScheduledMessages >= _options.Value.ProcessorBatchSize)
                    delay = _options.Value.ProcessorMinDelay;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling next message");
            }

            if (delay >= 10) await Task.Delay(delay, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatch(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var producer = scope.ServiceProvider.GetRequiredService<ProducerService<T>>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var processedScheduledMessageCount = 0;
        await using var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var set = dbContext.Set<ScheduledMessage>();

            var messages = await _messageFetcher.Fetch(set, currentTime, cancellationToken);

            if (messages.Count == 0)
                // No messages waiting.
                return 0;

            using var activity =
                AsyncMonolithInstrumentation.ActivitySource.StartActivity(AsyncMonolithInstrumentation
                    .ProcessScheduledMessageActivity);
            activity?.AddTag("scheduled_message.count", messages.Count);

            foreach (var message in messages)
            {
                producer.Produce(message);
                message.AvailableAfter = message.GetNextOccurrence(_timeProvider);
                processedScheduledMessageCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContextTransaction.CommitAsync(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Successfully scheduled message");
        }
        catch (Exception)
        {
            await dbContextTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        return processedScheduledMessageCount;
    }
}