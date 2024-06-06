using AsyncMonolith.Consumers;

namespace Demo.Spam;

public class SpamMessageConsumer : BaseConsumer<SpamMessage>
{
    private readonly TimeProvider _timeProvider;

    public SpamMessageConsumer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override Task Consume(SpamMessage message, CancellationToken cancellationToken)
    {
        if (message.Last)
        {
            SpamResultService.End = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        }

        SpamResultService.Count++;
        return Task.CompletedTask;
    }
}