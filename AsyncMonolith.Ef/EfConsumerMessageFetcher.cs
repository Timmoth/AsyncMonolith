using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

public sealed class EfConsumerMessageFetcher : IConsumerMessageFetcher
{
    private readonly IOptions<AsyncMonolithSettings> _options;

    public EfConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}