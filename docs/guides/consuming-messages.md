- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for a batch of available messages to process.
  - The query takes place at the frequency defined by `ProcessorMaxDelay`, if a full batch is returned it will delay by `ProcessorMinDelay`.
- **Concurrency**: Each app instance can run multiple parallel consumer processors defined by `ConsumerMessageProcessorCount`, unless using `AsyncMonolith.Ef`.
- **Batching**: Consumer messages will be read from the `consumer_messages` table in batches defined by `ConsumerMessageBatchSize`.
- **Idempotency**: Ensure your Consumers are idempotent, since they will be retried on failure.
- **Timeout**: Consumers timeout after the number of seconds defined by the `ConsumerTimeout` attribute or the `DefaultConsumerTimeout` if not set.

Example

```csharp
[ConsumerTimeout(5)] // Consumer timeouts after 5 seconds
public class DeleteUsersPosts : BaseConsumer<UserDeleted>
{
    private readonly ApplicationDbContext _dbContext;

    public ValueSubmittedConsumer(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override Task Consume(UserDeleted message, CancellationToken cancellationToken)
    {
        ...
		await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

## Consumer Failures 💢

- **Retry Logic**: Messages will be retried up to `MaxAttempts` times (with a `AttemptDelay` seconds between attempts) until they are moved to the `poisoned_messages` table.
- **Manual Intervention**: If a message is moved to the `poisoned_messages` table, it will need to be manually removed from the database or moved back to the `consumer_messages` table to be retried. Note that the poisoned message will only be retried a single time unless you set `attempts` back to 0.
- **Monitoring**: Periodically monitor the `poisoned_messages` table to ensure there are not too many failed messages.
