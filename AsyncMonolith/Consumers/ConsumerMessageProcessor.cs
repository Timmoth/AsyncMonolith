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
        ConsumerRegistry consumerRegistry, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory,
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
                _logger.LogError(ex, "Error processing consumer message batch.");
            }

            if (delay >= 10)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    ///     Fetch and Processes the next batch of consumer messages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the execution.</param>
    /// <returns>The number of processed messages.</returns>
    internal async Task<int> ProcessBatch(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        await using var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var consumerSet = dbContext.Set<ConsumerMessage>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var messages = await _consumerMessageFetcher.Fetch(consumerSet, currentTime, cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var tasks = new List<Task<(ConsumerMessage message, bool success)>>();
        foreach (var message in messages)
        {
            tasks.Add(Process(message, cancellationToken));
        }

        var processedMessageCount = 0;
        foreach (var (message, success) in await Task.WhenAll(tasks))
        {
            if (success)
            {
                processedMessageCount++;
                // Remove the message
                consumerSet.Remove(message);
            }
            else
            {
                // Increment the number of attempts
                message.Attempts++;
                if (message.Attempts < _options.Value.MaxAttempts)
                {
                    // Retry the message after a delay
                    message.AvailableAfter =
                        _timeProvider.GetUtcNow().ToUnixTimeSeconds() + _options.Value.AttemptDelay;
                }
                else
                {
                    // Move the message into the poisoned message table
                    consumerSet.Remove(message);
                    var poisonedSet = dbContext.Set<PoisonedMessage>();
                    poisonedSet.Add(new PoisonedMessage
                    {
                        Id = message.Id,
                        Attempts = message.Attempts,
                        AvailableAfter = message.AvailableAfter,
                        ConsumerType = message.ConsumerType,
                        CreatedAt = message.CreatedAt,
                        Payload = message.Payload,
                        PayloadType = message.PayloadType,
                        InsertId = message.InsertId
                    });
                }
            }
        }

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(cancellationToken);

        // Commit transaction
        await dbContextTransaction.CommitAsync(cancellationToken);

        return processedMessageCount;
    }

    /// <summary>
    ///     Processes a single consumer message.
    /// </summary>
    /// <param name="message">The consumer message to process.</param>
    /// <param name="cancellationToken">The cancellation token to stop the execution.</param>
    /// <returns>A tuple containing the processed consumer message and a flag indicating success.</returns>
    internal async Task<(ConsumerMessage message, bool success)> Process(ConsumerMessage message,
        CancellationToken cancellationToken = default)
    {
        using var activity =
            AsyncMonolithInstrumentation.ActivitySource.StartActivity(AsyncMonolithInstrumentation
                .ProcessConsumerMessageActivity);
        activity?.AddTag("consumer_message.id", message.Id);
        activity?.AddTag("consumer_message.attempt", message.Attempts + 1);
        activity?.AddTag("consumer_message.payload.type", message.PayloadType);
        activity?.AddTag("consumer_message.type", message.ConsumerType);

        try
        {
            var consumerType = _consumerRegistry.ResolveConsumerType(message);
            var consumerTimeout = _consumerRegistry.ResolveConsumerTimeout(message);

            var timeoutCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellationToken.CancelAfter(TimeSpan.FromSeconds(consumerTimeout));

            using var scope = _scopeFactory.CreateScope();

            // Resolve the consumer
            if (scope.ServiceProvider.GetRequiredService(consumerType)
                is not IConsumer consumer)
            {
                throw new Exception($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");
            }

            // Execute the consumer
            await consumer.Consume(message, timeoutCancellationToken.Token);

            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Successfully processed message for consumer: '{id}'", message.ConsumerType);
            return (message, true);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", nameof(ex));
            activity?.AddTag("exception.message", ex.Message);

            _logger.LogError(ex,
                message.Attempts > _options.Value.MaxAttempts
                    ? "Failed to consume message on attempt {attempt}, moving to poisoned messages."
                    : "Failed to consume message on attempt {attempt}, will retry.", message.Attempts);
        }

        return (message, false);
    }
}