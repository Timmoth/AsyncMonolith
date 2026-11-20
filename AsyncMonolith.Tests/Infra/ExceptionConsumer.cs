using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class ExceptionConsumer : IConsumer<ExceptionConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public ExceptionConsumer(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public Task Consume(ExceptionConsumerMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(ExceptionConsumer));
        throw new InvalidOperationException();
    }
}