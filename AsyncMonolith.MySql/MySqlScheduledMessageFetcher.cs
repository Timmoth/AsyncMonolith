using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AsyncMonolith.MySql;

public sealed class MySqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string MySql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public MySqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return set
            .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime),
                new MySqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}