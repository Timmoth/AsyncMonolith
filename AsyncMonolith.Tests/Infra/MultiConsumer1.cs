using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class MultiConsumer1 : IConsumer<MultiConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public MultiConsumer1(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public Task Consume(MultiConsumerMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(MultiConsumer1));
        return Task.CompletedTask;
    }
}