using System.Diagnostics;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Npgsql;

namespace AsyncMonolith.Consumers;

public sealed class ConsumerMessageProcessor<T> : BackgroundService where T : DbContext
{
    private const string PgSql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    private const string MySql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    private readonly ConsumerRegistry _consumerRegistry;
    private readonly ILogger<ConsumerMessageProcessor<T>> _logger;
    private readonly IOptions<AsyncMonolithSettings> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public ConsumerMessageProcessor(ILogger<ConsumerMessageProcessor<T>> logger,
        TimeProvider timeProvider,
        ConsumerRegistry consumerRegistry, IOptions<AsyncMonolithSettings> options, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _options = options;
        _scopeFactory = scopeFactory;
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
                {
                    delay = _options.Value.ProcessorMinDelay;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"Error processing consumer message batch.");
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
        var messages = _options.Value.DbType switch
        {
            DbType.Ef => await consumerSet
                .Where(m => m.AvailableAfter <= currentTime)
                .OrderBy(m => m.CreatedAt)
                .Take(_options.Value.ProcessorBatchSize)
                .ToListAsync(stoppingToken),
            DbType.PostgreSql => await consumerSet
                .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime), new NpgsqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
                .ToListAsync(stoppingToken),
            DbType.MySql => await consumerSet
                .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime), new MySqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
                .ToListAsync(stoppingToken),
            _ => throw new NotImplementedException("Consumer failed, invalid Db Type")
        };

        if (messages.Count == 0)
        {
            return 0;
        }

        var tasks = new List<Task<(ConsumerMessage, bool)>>();
        foreach (var message in messages)
        {
            tasks.Add(Process(message, stoppingToken));
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
                    message.AvailableAfter = _timeProvider.GetUtcNow().ToUnixTimeSeconds() + _options.Value.AttemptDelay;
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