using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerAttempts(2)]
public class ExceptionConsumer2Attempts : IConsumer<ExceptionConsumer2AttemptsMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public ExceptionConsumer2Attempts(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public Task Consume(ExceptionConsumer2AttemptsMessage message, ConsumeContext context, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(ExceptionConsumer2AttemptsMessage));
        throw new InvalidOperationException();
    }
}