using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerTimeout(1)]
public class TimeoutConsumer : BaseConsumer<TimeoutConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public TimeoutConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override async Task Consume(TimeoutConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(TimeoutConsumer));
        await Task.Delay(TimeSpan.FromSeconds(message.Delay), cancellationToken);
    }
}