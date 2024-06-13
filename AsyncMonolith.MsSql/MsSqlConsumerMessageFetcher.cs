using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql
{
    public class MsSqlConsumerMessageFetcher : ConsumerMessageFetcher
    {
        private const string MsSql = @"
                SELECT TOP (@batchSize) * 
                FROM consumer_messages WITH (ROWLOCK, READPAST)
                WHERE available_after <= @currentTime 
                ORDER BY created_at";

        public MsSqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
        {
        }

        public override Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
            CancellationToken cancellationToken)
        {
            return consumerSet
                .FromSqlRaw(MsSql, new SqlParameter("@currentTime", currentTime),
                    new SqlParameter("@batchSize", Options.Value.ProcessorBatchSize))
                .ToListAsync(cancellationToken);
        }
    }
}