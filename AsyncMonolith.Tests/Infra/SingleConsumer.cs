using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class SingleConsumer : BaseConsumer<SingleConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public SingleConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(SingleConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(SingleConsumer));
        return Task.CompletedTask;
    }
}