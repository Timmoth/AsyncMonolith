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
/// <summary>Ignores its cancellation token, as a badly written consumer might.</summary>
public class StubbornMessage : IConsumerPayload
{
}

[ConsumerTimeout(1)]
public class StubbornConsumer(TestConsumerInvocations invocations) : IConsumer<StubbornMessage>
{
    public async Task Consume(StubbornMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        invocations.Increment(nameof(StubbornConsumer));
#pragma warning disable CA2016 // Deliberately not passing the token
        await Task.Delay(TimeSpan.FromSeconds(20));
#pragma warning restore CA2016
    }
}
