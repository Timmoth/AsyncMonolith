using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerTimeout(1)]
public class TimeoutConsumer : IConsumer<TimeoutConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public TimeoutConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public async Task Consume(TimeoutConsumerMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(TimeoutConsumer));
        await Task.Delay(TimeSpan.FromSeconds(message.Delay), cancellationToken);
    }
}