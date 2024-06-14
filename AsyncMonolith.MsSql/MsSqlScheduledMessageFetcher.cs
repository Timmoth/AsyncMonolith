using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql;

public sealed class MsSqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string MsSql = @"
                        SELECT TOP (@batchSize) * 
                        FROM scheduled_messages WITH (ROWLOCK, READPAST)
                        WHERE available_after <= @currentTime";

    private readonly IOptions<AsyncMonolithSettings> _options;

    public MsSqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return set
            .FromSqlRaw(MsSql, new SqlParameter("@currentTime", currentTime),
                new SqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}