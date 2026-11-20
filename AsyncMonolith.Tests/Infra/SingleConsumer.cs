using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerTimeout(1)]
[ConsumerAttempts(2)]
public class SingleConsumer : IConsumer<SingleConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public SingleConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public Task Consume(SingleConsumerMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(SingleConsumer));
        return Task.CompletedTask;
    }
}