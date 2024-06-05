using System.Diagnostics;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Npgsql;

namespace AsyncMonolith.Scheduling;

public sealed class ScheduledMessageProcessor<T> : BackgroundService where T : DbContext
{
    private const int MaxChainLength = 10;

    private const string PgSql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT 1";

    private const string MySql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    LIMIT 1 
                    FOR UPDATE SKIP LOCKED";

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

    internal async Task<bool> ConsumeNext(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>();
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        await using var dbContextTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var set = dbContext.Set<ScheduledMessage>();

            var message = _options.Value.DbType switch
            {
                DbType.Ef => await set
                    .Where(m => m.AvailableAfter <= currentTime)
                    .OrderBy(m => Guid.NewGuid())
                    .FirstOrDefaultAsync(cancellationToken),
                DbType.PostgreSql => await set
                    .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime))
                    .FirstOrDefaultAsync(cancellationToken),
                DbType.MySql => await set
                    .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime))
                    .FirstOrDefaultAsync(cancellationToken),
                _ => throw new NotImplementedException("Scheduled Message Processor failed, invalid Db Type")
            };

            if (message == null)
                // No messages waiting.
                return false;

            using var activity =
                AsyncMonolithInstrumentation.ActivitySource.StartActivity(AsyncMonolithInstrumentation
                    .ProcessScheduledMessageActivity);
            activity?.AddTag("scheduled_message.id", message.Id);
            activity?.AddTag("scheduled_message.chron.expression", message.ChronExpression);
            activity?.AddTag("scheduled_message.chron.timezone", message.ChronTimezone);
            activity?.AddTag("scheduled_message.payload.type", message.PayloadType);
            activity?.AddTag("scheduled_message.tag", message.Tag);

            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<T>>();
            producer.Produce(message);

            message.AvailableAfter = message.GetNextOccurrence(_timeProvider);
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContextTransaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Successfully scheduled message of type: '{payload}'", message.PayloadType);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception)
        {
            await dbContextTransaction.RollbackAsync(cancellationToken);
            throw;
        }

        return true;
    }
}