using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public sealed class PostgreSqlProducerService<T> : IProducerService where T : DbContext
{
    private readonly ConsumerRegistry _consumerRegistry;
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public PostgreSqlProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _consumerRegistry = consumerRegistry;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public async Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null,
        CancellationToken cancellationToken = default)
        where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        var payloadType = typeof(TK).Name;
        insertId ??= _idGenerator.GenerateId();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter),
            new("@payload_type", payloadType),
            new("@payload", payload),
            new("@insert_id", insertId),
            new("@trace_id", traceId),
            new("@span_id", spanId)
        };

        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        for (var index = 0; index < consumerTypes.Count; index++)
        {
            if (sqlBuilder.Length > 0)
            {
                sqlBuilder.Append(", ");
            }

            sqlBuilder.Append(
                $@"(@id_{index}, @created_at, @available_after, 0, @consumer_type_{index}, @payload_type, @payload, @insert_id, @trace_id, @span_id)");

            parameters.Add(new NpgsqlParameter($"@id_{index}", _idGenerator.GenerateId()));
            parameters.Add(new NpgsqlParameter($"@consumer_type_{index}", consumerTypes[index]));
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id, trace_id, span_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    public async Task ProduceList<TK>(List<TK> messages, long? availableAfter = null,
        CancellationToken cancellationToken = default) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter),
            new("@trace_id", traceId),
            new("@span_id", spanId)
        };

        var payloadType = typeof(TK).Name;
        var consumerTypes = _consumerRegistry.ResolvePayloadConsumerTypes(payloadType);

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var insertId = _idGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);
            parameters.Add(new NpgsqlParameter($"@insert_id_{i}", insertId));
            parameters.Add(new NpgsqlParameter($"@payload_type_{i}", payloadType));
            parameters.Add(new NpgsqlParameter($"@payload_{i}", payload));

            for (var index = 0; index < consumerTypes.Count; index++)
            {
                if (sqlBuilder.Length > 0)
                {
                    sqlBuilder.Append(", ");
                }

                sqlBuilder.Append(
                    $@"(@id_{i}_{index}, @created_at, @available_after, 0, @consumer_type_{i}_{index}, @payload_type_{i}, @payload_{i}, @insert_id_{i}, @trace_id, @span_id)");

                parameters.Add(new NpgsqlParameter($"@id_{i}_{index}", _idGenerator.GenerateId()));
                parameters.Add(new NpgsqlParameter($"@consumer_type_{i}_{index}", consumerTypes[index]));
            }
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id, trace_id, span_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    public void Produce(ScheduledMessage message)
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var set = _dbContext.Set<ConsumerMessage>();
        var insertId = _idGenerator.GenerateId();
        foreach (var consumerId in _consumerRegistry.ResolvePayloadConsumerTypes(message.PayloadType))
        {
            set.Add(new ConsumerMessage
            {
                Id = _idGenerator.GenerateId(),
                CreatedAt = currentTime,
                AvailableAfter = currentTime,
                ConsumerType = consumerId,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Attempts = 0,
                InsertId = insertId,
                TraceId = null,
                SpanId = null
            });
        }
    }
}