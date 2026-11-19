using System.Diagnostics;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Background service which fetches and processes batches of consumer messages.
/// </summary>
/// <typeparam name="T">The type of the DbContext.</typeparam>
public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly IConsumerMessageFetcher _consumerMessageFetcher;
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    private int _railId;
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsumerMessageProcessor{T}" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="consumerRegistry">The consumer registry.</param>
    /// <param name="options">The options.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="consumerMessageFetcher">The consumer message fetcher.</param>
    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry,
        IOptions<AsyncMonolithSettings> options,
        IServiceScopeFactory scopeFactory,
        IConsumerMessageFetcher consumerMessageFetcher)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _options = options;
        _scopeFactory = scopeFactory;
        _consumerMessageFetcher = consumerMessageFetcher;
    }

    /// <summary>
    /// Sets the rail id of this consumer message processor
    /// </summary>
    /// <param name="railId"></param>
    public void SetRailId(int railId)
    {
        _railId = railId;
    }

    /// <summary>
    ///     Asynchronously fetches batches of messages before processing them.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token to stop the execution.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _options.Value.ProcessorMaxDelay;
            try
            {
                var processedMessageCount = await ProcessBatch(stoppingToken);
                if (processedMessageCount == _options.Value.ProcessorBatchSize)
                {
                    delay = _options.Value.ProcessorMinDelay;
                }
            }
            catch (Exception ex)
            {
                _logger.ConsumerBatchFailed(ex);
            }

            if (delay >= 10)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    ///     Seconds a claimed message stays leased beyond its consumer timeout. If a processor stops before
    ///     finishing a message, the message becomes available again once the lease runs out.
    /// </summary>
    internal const int LeaseGracePeriodSeconds = 30;

    /// <summary>
    ///     Claims and processes the next batch of consumer messages.
    /// </summary>
    /// <remarks>
    ///     Messages are claimed in one short transaction: each is leased by moving its AvailableAfter past
    ///     the consumer timeout, and the attempt is counted. Consumers then run outside any transaction, so
    ///     they can write to the database freely (SQLite allows only one writer at a time). Each message is
    ///     then completed in its own short write, guarded by its lease.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token to stop the execution.</param>
    /// <returns>The number of messages processed successfully.</returns>
    internal async Task<int> ProcessBatch(CancellationToken cancellationToken)
    {
        var messages = await ClaimBatch(cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        var tasks = new List<Task<List<bool>>>();
        foreach (var consumerMessageGroup in messages.GroupBy(m => m.ConsumerType))
        {
            var group = consumerMessageGroup.ToList();
            var executionMode = _consumerRegistry.ResolveConsumerExecutionMode(consumerMessageGroup.Key);

            if (executionMode == ConsumerInstanceExecutionMode.Parallel)
            {
                // Parallel: all messages in the group are processed concurrently
                tasks.Add(Task.WhenAll(group.Select(m => ProcessAndComplete(m, cancellationToken)))
                    .ContinueWith(t => t.Result.ToList(), cancellationToken, TaskContinuationOptions.None,
                        TaskScheduler.Default));
            }
            else
            {
                // Sequential: one message at a time in order
                tasks.Add(Task.Run(async () =>
                {
                    var results = new List<bool>();
                    foreach (var message in group)
                    {
                        results.Add(await ProcessAndComplete(message, cancellationToken));
                    }

                    return results;
                }, cancellationToken));
            }
        }

        return (await Task.WhenAll(tasks)).SelectMany(r => r).Count(success => success);
    }

    /// <summary>
    ///     Fetches the next batch for this rail and leases it in one short transaction.
    /// </summary>
    /// <returns>The claimed messages, with their attempt counted and their lease in AvailableAfter.</returns>
    internal async Task<List<ConsumerMessage>> ClaimBatch(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var messages = await _consumerMessageFetcher.Fetch(dbContext.Set<ConsumerMessage>(), currentTime, _railId,
            cancellationToken);
        if (messages.Count == 0)
        {
            return messages;
        }

        foreach (var message in messages)
        {
            message.Attempts++;
            message.AvailableAfter = currentTime + _consumerRegistry.ResolveConsumerTimeout(message) +
                                     LeaseGracePeriodSeconds;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    private async Task<bool> ProcessAndComplete(ConsumerMessage message, CancellationToken cancellationToken)
    {
        var error = await Process(message, cancellationToken);

        // Record the outcome even when stopping, so the attempt isn't left waiting for its lease to expire.
        await Complete(message, error is null, CancellationToken.None, error);
        return error is null;
    }

    /// <summary>
    ///     Records the outcome of a claimed message: removes it on success, otherwise schedules a retry or
    ///     moves it to the poisoned messages. Does nothing if the lease has since been taken over.
    /// </summary>
    internal async Task Complete(ConsumerMessage message, bool success, CancellationToken cancellationToken,
        string? error = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var leased = dbContext.Set<ConsumerMessage>()
            .Where(m => m.Id == message.Id && m.AvailableAfter == message.AvailableAfter);

        if (success)
        {
            await leased.ExecuteDeleteAsync(cancellationToken);
            return;
        }

        if (message.Attempts < _consumerRegistry.ResolveConsumerMaxAttempts(message))
        {
            var retryAt = _timeProvider.GetUtcNow().ToUnixTimeSeconds() +
                          _options.Value.GetRetryDelay(message.Attempts);
            await leased.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.AvailableAfter, retryAt)
                .SetProperty(m => m.LastError, error), cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await leased.ExecuteDeleteAsync(cancellationToken) == 0)
        {
            return;
        }

        dbContext.Set<PoisonedMessage>().Add(new PoisonedMessage
        {
            Id = message.Id,
            Attempts = message.Attempts,
            AvailableAfter = message.AvailableAfter,
            ConsumerType = message.ConsumerType,
            CreatedAt = message.CreatedAt,
            Payload = message.Payload,
            PayloadType = message.PayloadType,
            InsertId = message.InsertId,
            TraceId = message.TraceId,
            SpanId = message.SpanId,
            RailId = message.RailId,
            LastError = error
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Activity? StartActivity(ConsumerMessage message)
    {
        if (string.IsNullOrEmpty(message.TraceId) || string.IsNullOrEmpty(message.SpanId))
        {
            return AsyncMonolithInstrumentation.ActivitySource.StartActivity(
                AsyncMonolithInstrumentation.ProcessConsumerMessageActivity,
                kind: ActivityKind.Internal);
        }

        var activityContext = new ActivityContext(
            ActivityTraceId.CreateFromString(message.TraceId),
            ActivitySpanId.CreateFromString(message.SpanId),
            ActivityTraceFlags.Recorded);

        return AsyncMonolithInstrumentation.ActivitySource.StartActivity(
                AsyncMonolithInstrumentation.ProcessConsumerMessageActivity,
                parentContext: activityContext,
                kind: ActivityKind.Internal);
    }

    /// <summary>
    ///     Processes a single consumer message.
    /// </summary>
    /// <param name="message">The consumer message to process.</param>
    /// <param name="cancellationToken">The cancellation token to stop the execution.</param>
    /// <returns>Null on success, otherwise a description of the error.</returns>
    internal async Task<string?> Process(ConsumerMessage message, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(message);

        activity?.AddTag("consumer_message.id", message.Id);
        activity?.AddTag("consumer_message.attempt", message.Attempts);
        activity?.AddTag("consumer_message.payload.type", message.PayloadType);
        activity?.AddTag("consumer_message.type", message.ConsumerType);

        var consumerTimeout = _consumerRegistry.ResolveConsumerTimeout(message);
        try
        {
            var consumerType = _consumerRegistry.ResolveConsumerType(message);

            using var timeoutCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellationToken.CancelAfter(TimeSpan.FromSeconds(consumerTimeout));

            using var scope = _scopeFactory.CreateScope();

            // Resolve the consumer
            if (scope.ServiceProvider.GetRequiredService(consumerType)
                is not IConsumer consumer)
            {
                throw new InvalidOperationException($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            }

            // Execute the consumer
            await consumer.Consume(message, timeoutCancellationToken.Token);

            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.ConsumerMessageProcessed(message.ConsumerType);
            return null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.ConsumerMessageFailed(ex, message.Attempts);
            return $"Timed out after {consumerTimeout} seconds.";
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);
            _logger.ConsumerMessageFailed(ex, message.Attempts);
            return Describe(ex);
        }
    }

    private const int MaxErrorLength = 2000;

    private static string Describe(Exception exception)
    {
        var description = $"{exception.GetType().FullName}: {exception.Message}";
        return description.Length <= MaxErrorLength ? description : description[..MaxErrorLength];
    }
}