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
internal sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ILogger<ScheduledMessageProcessor<T>> _logger;
    private readonly IScheduledMessageFetcher _messageFetcher;
    private readonly IOptions<AsyncMonolithOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncMonolithMetrics _metrics;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScheduledMessageProcessor{T}" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="options">The options.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="messageFetcher">The scheduled message fetcher.</param>
    /// <param name="metrics">The metrics.</param>
    public ScheduledMessageProcessor(ILogger<ScheduledMessageProcessor<T>> logger,
        TimeProvider timeProvider, IOptions<AsyncMonolithOptions> options, IServiceScopeFactory scopeFactory,
        IScheduledMessageFetcher messageFetcher, AsyncMonolithMetrics metrics)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _options = options;
        _scopeFactory = scopeFactory;
        _messageFetcher = messageFetcher;
        _metrics = metrics;
    }

    /// <summary>
    ///     Executes the background service asynchronously.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token to stop the execution.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessBatch(stoppingToken);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Back off while batches keep failing (for example while the database is unavailable).
                consecutiveFailures++;
                _logger.ScheduleBatchFailed(ex);
            }

            var delay = ProcessorPolling.NextDelay(_options.Value, processed, consecutiveFailures);
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>How long to wait before checking a schedule that couldn't be evaluated again.</summary>
    internal const int InvalidScheduleRetrySeconds = 3600;

    /// <summary>
    ///     Processes a batch of scheduled messages. A schedule that fails is logged and moved on; it never stops
    ///     the others.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of messages produced.</returns>
    internal async Task<int> ProcessBatch(CancellationToken cancellationToken = default)
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
            {
                return 0;
            }

            using var activity =
                AsyncMonolithInstrumentation.ActivitySource.StartActivity(AsyncMonolithInstrumentation
                    .ProcessScheduledMessageActivity);
            activity?.AddTag("scheduled_message.count", messages.Count);

            foreach (var message in messages)
            {
                // A schedule that can't be evaluated (bad cron expression or time zone, or no future occurrence)
                // is checked again in an hour instead of blocking every other schedule in the batch.
                var dueAt = message.AvailableAfter;
                long next;
                Exception? invalid = null;
                try
                {
                    next = message.GetNextOccurrence(_timeProvider);
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException
                                               or TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    invalid = ex;
                    next = currentTime + InvalidScheduleRetrySeconds;
                }

                // Move the schedule on only if no other processor has already done so for this occurrence.
                var updated = await set
                    .Where(m => m.Id == message.Id && m.AvailableAfter == dueAt)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.AvailableAfter, next), cancellationToken);
                if (updated == 0)
                {
                    continue;
                }

                if (invalid is not null)
                {
                    _logger.ScheduleInvalid(invalid, message.Id, message.Tag);
                    continue;
                }

                try
                {
                    producer.ProduceScheduled(message);
                    processedScheduledMessageCount++;
                    _metrics.SchedulesFired.Add(1, new KeyValuePair<string, object?>("payload_type", message.PayloadType));
                }
                catch (InvalidOperationException ex)
                {
                    // No registered consumers for the payload (for example a module was removed): skip this
                    // occurrence, keep the schedule.
                    _logger.ScheduledOccurrenceSkipped(ex, message.Id, message.PayloadType);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContextTransaction.CommitAsync(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.ScheduledMessagesProduced();
        }
        catch (Exception)
        {
            await dbContextTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        return processedScheduledMessageCount;
    }
}