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
    private const int MaxChainLength = 10;

    private const string PgSql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT 1";

    private const string MySql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    LIMIT 1 
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
        var consumedMessageChainLength = 0;
        var deltaDelay = (_options.Value.ProcessorMaxDelay - _options.Value.ProcessorMinDelay) / MaxChainLength;
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

            var delay = _options.Value.ProcessorMaxDelay -
                        deltaDelay * Math.Clamp(consumedMessageChainLength, 0, MaxChainLength);
            if (delay >= 10) await Task.Delay(delay, stoppingToken);
        }
    }

    internal async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();

        await using var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var consumerSet = dbContext.Set<ConsumerMessage>();
            var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

            var message = _options.Value.DbType switch
            {
                DbType.Ef => await consumerSet
                    .Where(m => m.AvailableAfter <= currentTime)
                    .OrderBy(m => m.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken),
                DbType.PostgreSql => await consumerSet
                    .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime))
                    .FirstOrDefaultAsync(cancellationToken),
                DbType.MySql => await consumerSet
                    .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime))
                    .FirstOrDefaultAsync(cancellationToken),
                _ => throw new NotImplementedException("Consumer failed, invalid Db Type")
            };

            if (message == null)
                // No messages waiting.
                return false;

            using var activity =
                AsyncMonolithInstrumentation.ActivitySource.StartActivity(AsyncMonolithInstrumentation
                    .ProcessConsumerMessageActivity);
            activity?.AddTag("consumer_message.id", message.Id);
            activity?.AddTag("consumer_message.attempt", message.Attempts + 1);
            activity?.AddTag("consumer_message.payload.type", message.PayloadType);
            activity?.AddTag("consumer_message.type", message.ConsumerType);
            var failure = false;

            try
            {
                if (scope.ServiceProvider.GetRequiredService(_consumerRegistry.ResolveConsumerType(message))
                    is not IConsumer consumer)
                    throw new Exception($"Couldn't resolve consumer service of type: '{message.ConsumerType}'");

                await consumer.Consume(message, cancellationToken);
                consumerSet.Remove(message);
                await dbContext.SaveChangesAsync(cancellationToken);
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

                failure = true;
            }

            if (failure)
            {
                message.Attempts++;
                if (message.Attempts < _options.Value.MaxAttempts)
                {
                    message.AvailableAfter = currentTime + _options.Value.AttemptDelay;
                    activity?.AddTag("consumer_message.destination", "consumer_messages");
                }
                else
                {
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
                    activity?.AddTag("consumer_message.destination", "poisoned_messages");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                activity?.AddTag("consumer_message.destination", "removed");
            }

            await dbContextTransaction.CommitAsync(cancellationToken);

            if (!failure)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation("Successfully processed message for consumer: '{id}'", message.ConsumerType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming message");
        }

        return true;
    }
}