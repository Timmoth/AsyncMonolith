using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

public class EfConsumerMessageFetcher : ConsumerMessageFetcher
{
    public EfConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken)
    {
        return consumerSet
            .Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.CreatedAt)
            .Take(Options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}