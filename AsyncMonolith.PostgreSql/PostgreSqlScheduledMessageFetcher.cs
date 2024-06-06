using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

public class PostgreSqlScheduledMessageFetcher : ScheduledMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    public PostgreSqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken)
    {
        return set
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}