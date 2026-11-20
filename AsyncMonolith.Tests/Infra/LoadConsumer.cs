using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

public class LoadMessage : IConsumerPayload
{
    [JsonPropertyName("number")] public int Number { get; set; }
}

/// <summary>Shared by every app instance in a load test.</summary>
public sealed class LoadRecorder
{
    public ConcurrentDictionary<int, int> Successes { get; } = new();
    public ConcurrentDictionary<int, int> Attempts { get; } = new();
}

/// <summary>Fails the first attempt of every tenth message, so retries happen under load.</summary>
public class LoadConsumer(LoadRecorder recorder) : IConsumer<LoadMessage>
{
    public Task Consume(LoadMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        recorder.Attempts.AddOrUpdate(payload.Number, 1, (_, n) => n + 1);
        if (payload.Number % 10 == 0 && context.Attempt == 1)
        {
            throw new InvalidOperationException("Failing the first attempt on purpose");
        }

        recorder.Successes.AddOrUpdate(payload.Number, 1, (_, n) => n + 1);
        return Task.CompletedTask;
    }
}
