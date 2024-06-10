using System.Diagnostics;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Represents a background service for processing scheduled messages.
/// </summary>
/// <typeparam name="T">The type of the database context.</typeparam>
public sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ILogger<ScheduledMessageProcessor<T>> _logger;
    private readonly ScheduledMessageFetcher _messageFetcher;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScheduledMessageProcessor{T}" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="options">The options.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="messageFetcher">The scheduled message fetcher.</param>
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

    /// <summary>
    ///     Executes the background service asynchronously.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token to stop the execution.</param>
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

    /// <summary>
    ///     Processes a batch of scheduled messages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of processed scheduled messages.</returns>
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