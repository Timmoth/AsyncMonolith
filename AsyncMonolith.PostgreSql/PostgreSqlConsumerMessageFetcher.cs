using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public class PostgreSqlConsumerMessageFetcher : ConsumerMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    public PostgreSqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken)
    {
        return consumerSet
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}