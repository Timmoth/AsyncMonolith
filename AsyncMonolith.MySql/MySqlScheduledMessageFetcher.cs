using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AsyncMonolith.MySql;

public class MySqlScheduledMessageFetcher : ScheduledMessageFetcher
{
    private const string MySql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    public MySqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken)
    {
        return set
            .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime),
                new MySqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}