using AsnyMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class MultiConsumer2 : BaseConsumer<MultiConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public MultiConsumer2(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(MultiConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(MultiConsumer2));

        return Task.CompletedTask;
    }
}