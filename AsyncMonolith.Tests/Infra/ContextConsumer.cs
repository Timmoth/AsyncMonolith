using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

/// <summary>Records every context it receives, and fails while <see cref="ContextRecorder.FailuresLeft" /> &gt; 0.</summary>
public sealed class ContextRecorder
{
    public List<ConsumeContext> Contexts { get; } = [];
    public int FailuresLeft { get; set; }
}

public class ContextMessage : IConsumerPayload
{
}

[ConsumerAttempts(3)]
public class ContextConsumer(ContextRecorder recorder) : IConsumer<ContextMessage>
{
    public Task Consume(ContextMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        recorder.Contexts.Add(context);
        if (recorder.FailuresLeft > 0)
        {
            recorder.FailuresLeft--;
            throw new InvalidOperationException("Failing as asked");
        }

        return Task.CompletedTask;
    }
}
