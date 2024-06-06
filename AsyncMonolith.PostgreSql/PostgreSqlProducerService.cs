using System.Text;
using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public class PostgreSqlProducerService<T> : ProducerService<T> where T : DbContext
{
    public PostgreSqlProducerService(TimeProvider timeProvider, ConsumerRegistry consumerRegistry, T dbContext,
        IAsyncMonolithIdGenerator idGenerator) : base(timeProvider, consumerRegistry, dbContext, idGenerator)
    {
    }

    public override async Task Produce<TK>(TK message, long? availableAfter = null, string? insertId = null)
    {
        var currentTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;
        var payload = JsonSerializer.Serialize(message);

        var payloadType = typeof(TK).Name;
        insertId ??= IdGenerator.GenerateId();

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter),
            new("@payload_type", payloadType),
            new("@payload", payload),
            new("@insert_id", insertId)
        };

        var consumerTypes = ConsumerRegistry.ResolvePayloadConsumerTypes(payloadType);
        for (var index = 0; index < consumerTypes.Count; index++)
        {
            if (sqlBuilder.Length > 0) sqlBuilder.Append(", ");

            sqlBuilder.Append(
                $@"(@id_{index}, @created_at, @available_after, 0, @consumer_type_{index}, @payload_type, @payload, @insert_id)");

            parameters.Add(new NpgsqlParameter($"@id_{index}", IdGenerator.GenerateId()));
            parameters.Add(new NpgsqlParameter($"@consumer_type_{index}", consumerTypes[index]));
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await DbContext.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    public override async Task ProduceList<TK>(List<TK> messages, long? availableAfter = null)
    {
        var currentTime = TimeProvider.GetUtcNow().ToUnixTimeSeconds();
        availableAfter ??= currentTime;

        var sqlBuilder = new StringBuilder();
        var parameters = new List<NpgsqlParameter>
        {
            new("@created_at", currentTime),
            new("@available_after", availableAfter)
        };

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var insertId = IdGenerator.GenerateId();
            var payload = JsonSerializer.Serialize(message);
            var payloadType = typeof(TK).Name;
            parameters.Add(new NpgsqlParameter($"@insert_id_{i}", insertId));
            parameters.Add(new NpgsqlParameter($"@payload_type_{i}", payloadType));
            parameters.Add(new NpgsqlParameter($"@payload_{i}", payload));

            var consumerTypes = ConsumerRegistry.ResolvePayloadConsumerTypes(payloadType);
            for (var index = 0; index < consumerTypes.Count; index++)
            {
                if (sqlBuilder.Length > 0) sqlBuilder.Append(", ");

                sqlBuilder.Append(
                    $@"(@id_{i}_{index}, @created_at, @available_after, 0, @consumer_type_{i}_{index}, @payload_type_{i}, @payload_{i}, @insert_id_{i})");

                parameters.Add(new NpgsqlParameter($"@id_{i}_{index}", IdGenerator.GenerateId()));
                parameters.Add(new NpgsqlParameter($"@consumer_type_{i}_{index}", consumerTypes[index]));
            }
        }

        var sql = $@"
        INSERT INTO consumer_messages (id, created_at, available_after, attempts, consumer_type, payload_type, payload, insert_id) 
        VALUES {sqlBuilder} 
        ON CONFLICT (insert_id, consumer_type) DO NOTHING;";

        await DbContext.Database.ExecuteSqlRawAsync(sql, parameters);
    }
}