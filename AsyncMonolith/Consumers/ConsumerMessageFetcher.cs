using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Consumers;

public abstract class ConsumerMessageFetcher
{
    protected readonly IOptions<AsyncMonolithSettings> Options;

    protected ConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        Options = options;
    }

    public abstract Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken);
}