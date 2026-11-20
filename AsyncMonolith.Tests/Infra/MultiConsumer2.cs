using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class MultiConsumer2 : IConsumer<MultiConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public MultiConsumer2(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public Task Consume(MultiConsumerMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(MultiConsumer2));

        return Task.CompletedTask;
    }
}