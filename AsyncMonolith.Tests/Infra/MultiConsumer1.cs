using AsnyMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class MultiConsumer1 : BaseConsumer<MultiConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public MultiConsumer1(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(MultiConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(MultiConsumer1));
        return Task.CompletedTask;
    }
}