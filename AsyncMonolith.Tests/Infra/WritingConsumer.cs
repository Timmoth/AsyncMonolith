using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;

namespace AsyncMonolith.Tests.Infra;

public class WritingConsumerMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}

/// <summary>
///     A consumer that writes to the database, as most real consumers do
///     (here by producing a follow-up message and saving it).
/// </summary>
[ConsumerTimeout(10)]
[ConsumerAttempts(1)]
public class WritingConsumer : BaseConsumer<WritingConsumerMessage>
{
    private readonly TestDbContext _dbContext;
    private readonly IProducerService _producer;

    public WritingConsumer(TestDbContext dbContext, IProducerService producer)
    {
        _dbContext = dbContext;
        _producer = producer;
    }

    public override async Task Consume(WritingConsumerMessage message, CancellationToken cancellationToken)
    {
        await _producer.Produce(new SingleConsumerMessage { Name = message.Name },
            cancellationToken: cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
