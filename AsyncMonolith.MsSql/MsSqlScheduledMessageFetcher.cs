using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql
{
    public class MsSqlScheduledMessageFetcher : ScheduledMessageFetcher
    {
        private const string MsSql = @"
                        SELECT TOP (@batchSize) * 
                        FROM scheduled_messages WITH (ROWLOCK, READPAST)
                        WHERE available_after <= @currentTime";

        public MsSqlScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
        {
        }

        public override Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
            CancellationToken cancellationToken)
        {
            return set
                .FromSqlRaw(MsSql, new SqlParameter("@currentTime", currentTime),
                    new SqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
                .ToListAsync(cancellationToken);
        }
    }
}