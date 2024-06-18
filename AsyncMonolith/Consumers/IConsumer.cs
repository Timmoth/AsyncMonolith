namespace AsyncMonolith.Consumers;

public interface IConsumer
{
    public Task Consume(ConsumerMessage message, CancellationToken cancellationToken);
}