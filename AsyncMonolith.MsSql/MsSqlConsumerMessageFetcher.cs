using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql;

public sealed class MsSqlConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string MsSql = @"
                SELECT TOP (@batchSize) * 
                FROM consumer_messages WITH (ROWLOCK, READPAST)
                WHERE available_after <= @currentTime 
                ORDER BY created_at";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public MsSqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .FromSqlRaw(MsSql, new SqlParameter("@currentTime", currentTime),
                new SqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}