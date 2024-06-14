using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public sealed class PostgreSqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public PostgreSqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return set
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}