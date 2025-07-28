using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class RailConsumerMessage : IConsumerPayload
{
    
}

public class RailConsumer0 : BaseConsumer<RailConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public RailConsumer0(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(RailConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(RailConsumer0));

        return Task.CompletedTask;
    }
}

[ConsumerRailIdAttribute(1)]
public class RailConsumer1 : BaseConsumer<RailConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public RailConsumer1(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(RailConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(RailConsumer1));

        return Task.CompletedTask;
    }
}

[ConsumerRailId(2)]
public class RailConsumer2 : BaseConsumer<RailConsumerMessage>
{
    private readonly TestConsumerInvocations _consumerInvocations;

    public RailConsumer2(TestConsumerInvocations consumerInvocations)
    {
        _consumerInvocations = consumerInvocations;
    }

    public override Task Consume(RailConsumerMessage message, CancellationToken cancellationToken)
    {
        _consumerInvocations.Increment(nameof(RailConsumer2));

        return Task.CompletedTask;
    }
}