using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AsyncMonolith.MySql;

public sealed class MySqlConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string MySql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public MySqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime),
                new MySqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}