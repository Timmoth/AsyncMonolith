using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public sealed class PostgreSqlConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public PostgreSqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}