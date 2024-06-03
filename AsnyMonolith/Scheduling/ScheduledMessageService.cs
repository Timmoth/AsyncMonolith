using System.Text.Json;
using AsnyMonolith.Consumers;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsnyMonolith.Scheduling;

public sealed class ScheduledMessageService<T> where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ScheduledMessageService(TimeProvider timeProvider, T dbContext, IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public string Schedule<TK>(TK message, long delay, params string[] tags) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var payload = JsonSerializer.Serialize(message);
        var id = _idGenerator.GenerateId();
        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = id,
            PayloadType = typeof(TK).Name,
            AvailableAfter = currentTime + delay,
            Tags = tags,
            Delay = delay,
            Payload = payload
        });

        return id;
    }

    public async Task DeleteByTag(string tag, CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        await set.Where(t => t.Tags.Contains(tag)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteById(string id, CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        await set.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}