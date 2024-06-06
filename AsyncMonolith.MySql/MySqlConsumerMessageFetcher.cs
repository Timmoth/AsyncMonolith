using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AsyncMonolith.MySql;

public class MySqlConsumerMessageFetcher : ConsumerMessageFetcher
{
    private const string MySql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    public MySqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken)
    {
        return consumerSet
            .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime),
                new MySqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}