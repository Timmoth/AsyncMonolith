namespace AsnyMonolith.Consumers;

public interface IConsumer
{
    public Task Consume(ConsumerMessage message, CancellationToken token);
}