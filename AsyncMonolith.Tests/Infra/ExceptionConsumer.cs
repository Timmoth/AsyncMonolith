using AsnyMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class ExceptionConsumer : BaseConsumer<ExceptionConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public ExceptionConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(ExceptionConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(ExceptionConsumer));
        throw new Exception();
    }
}