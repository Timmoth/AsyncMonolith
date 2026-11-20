using AsyncMonolith.Consumers;

namespace Example.Api.Spam;

public class SpamMessageConsumer : IConsumer<SpamMessage>
{
    private readonly TimeProvider _timeProvider;

    public SpamMessageConsumer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task Consume(SpamMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        if (message.Last)
        {
            SpamResultService.End = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        }

        SpamResultService.Count++;
        return Task.CompletedTask;
    }
}