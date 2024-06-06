using System.Diagnostics;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Consumers;

public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private readonly ConsumerMessageFetcher _consumerMessageFetcher;
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory,
        ConsumerMessageFetcher consumerMessageFetcher)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _options = options;
        _scopeFactory = scopeFactory;
        _consumerMessageFetcher = consumerMessageFetcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _options.Value.ProcessorMaxDelay;
            try
            {
                var processedMessageCount = await ProcessBatch(stoppingToken);
                if (processedMessageCount == _options.Value.ProcessorBatchSize)
                    delay = _options.Value.ProcessorMinDelay;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing consumer message batch.");
            }

            if (delay >= 10) await Task.Delay(delay, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatch(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        await using var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
        var consumerSet = dbContext.Set<ConsumerMessage>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var messages = await _consumerMessageFetcher.Fetch(consumerSet, currentTime, stoppingToken);

        if (messages.Count == 0) return 0;

        var tasks = new List<Task<(ConsumerMessage, bool)>>();
        foreach (var message in messages) tasks.Add(Process(message, stoppingToken));

        var processedMessageCount = 0;
        foreach (var (message, success) in await Task.WhenAll(tasks))
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
                        PayloadType = message.PayloadType
                    });
                }
            }

        // Save changes to the message tables
        await dbContext.SaveChangesAsync(stoppingToken);

        // Commit transaction
        await dbContextTransaction.CommitAsync(stoppingToken);

        return processedMessageCount;
    }

    internal async Task<(ConsumerMessage, bool)> Process(ConsumerMessage message, CancellationToken cancellationToken)
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
            using var scope = _scopeFactory.CreateScope();

            // Resolve the consumer
            if (scope.ServiceProvider.GetRequiredService(_consumerRegistry.ResolveConsumerType(message))
                is not IConsumer consumer)
                throw new Exception($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");

            // Execute the consumer
            await consumer.Consume(message, cancellationToken);

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
                message.Attempts + 1 >= _options.Value.MaxAttempts
                    ? "Failed to consume message on attempt {attempt}, moving to poisoned messages."
                    : "Failed to consume message on attempt {attempt}, will retry.", message.Attempts);
        }

        return (message, false);
    }
}