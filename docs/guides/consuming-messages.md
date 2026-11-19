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
[ConsumerAttempts(1)] // Consumer messages moved to poisoned table after 1 failed attempt
public class DeleteUsersPosts : BaseConsumer<UserDeleted>
{
    private readonly ApplicationDbContext _dbContext;

    public DeleteUsersPosts(ApplicationDbContext dbContext)
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

- **Retry Logic**: Messages are retried up to `MaxAttempts` times before they are moved to the `poisoned_messages` table. Add the `[ConsumerAttempts(1)]` attribute to override this per consumer.
- **Backoff**: The first retry waits `AttemptDelay` seconds (default 10), and each later retry waits twice as long as the one before, up to `MaxAttemptDelay` seconds (default 300). With the defaults a message is retried after 10, 20, 40 and 80 seconds.
- **Errors**: The error from the most recent failed attempt is stored in `last_error` on both tables, for example `System.InvalidOperationException: ...` or `Timed out after 10 seconds.`
- **Interrupted processing**: A message is leased while its consumer runs. If the app stops mid-message, the message becomes available again once its consumer timeout plus 30 seconds has passed, and the interrupted attempt counts.
- **Manual Intervention**: Use `IPoisonedMessageService` to count, list, retry or delete poisoned messages. Retrying moves a message back to `consumer_messages` with its attempts reset. Changes are saved immediately.
- **Monitoring**: Periodically monitor the `poisoned_messages` table to ensure there are not too many failed messages.

```csharp
public class FailedMessagesController(IPoisonedMessageService poisoned) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<PoisonedMessage>> List() => poisoned.List(skip: 0, take: 50);
    [HttpPost("{id}/retry")] public Task<bool> Retry(string id) => poisoned.Retry(id);
    [HttpDelete("{id}")] public Task<bool> Delete(string id) => poisoned.Delete(id);
}
```
