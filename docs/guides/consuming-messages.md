- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for a batch of available messages to process.
  - After a batch that wasn't full it waits `IdlePollInterval` before looking again; after a full batch it waits `BusyPollInterval`.
- **Multiple instances**: Any number of app instances can process the same tables, with every database package. A processor claims each message with a conditional update, so a message is only ever claimed by one processor at a time, and each schedule occurrence fires once. The PostgreSQL, SQL Server, MySQL and MariaDB packages also lock rows while claiming, so processors skip each other's batches instead of competing for them.
- **Rails**: Each rail (`[ConsumerRailId]`) gets its own processor, so a slow consumer on one rail doesn't hold up consumers on another.
- **Batching**: Consumer messages will be read from the `consumer_messages` table in batches defined by `BatchSize`.
- **Idempotency**: Ensure your Consumers are idempotent, since they will be retried on failure.
- **Timeout**: Consumers timeout after the number of seconds defined by the `ConsumerTimeout` attribute or the `DefaultConsumerTimeout` if not set.

Consumers implement `IConsumer<TPayload>`. The `ConsumeContext` describes the message and this attempt: `MessageId`, `DeduplicationKey`, `Attempt` (starting at 1), `MaxAttempts`, `IsLastAttempt`, `CreatedAt`, `ConsumerName`, `PayloadName` and `TraceId`. Use them for idempotency checks and logging.

Example

```csharp
[ConsumerTimeout(5)] // Consumer timeouts after 5 seconds
[ConsumerAttempts(1)] // Consumer messages moved to poisoned table after 1 failed attempt
public class DeleteUsersPosts : IConsumer<UserDeleted>
{
    private readonly ApplicationDbContext _dbContext;

    public DeleteUsersPosts(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task Consume(UserDeleted message, ConsumeContext context, CancellationToken cancellationToken)
    {
        ...
		await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

## Ordering

Messages are taken roughly oldest first, but **delivery order is not guaranteed**:

- `created_at` is stored in whole seconds. Messages created in the same second are taken in `id` order, and ids are random, so their order is effectively arbitrary.
- A failed message is retried later, after messages that were created after it.
- Messages in a batch run in parallel by default. `[ConsumerExecutionMode(ConsumerExecutionMode.Sequential)]` only runs one consumer's messages one at a time **within a batch on one processor**. It doesn't order them across batches, retries, rails or app instances.

Design consumers so the order of messages doesn't matter, for example by checking the current state of the data rather than relying on earlier messages having run.

## Consumer Failures 💢

- **Retry Logic**: Messages are retried up to `MaxAttempts` times before they are moved to the `poisoned_messages` table. Add the `[ConsumerAttempts(1)]` attribute to override this per consumer.
- **Backoff**: The first retry waits `AttemptDelay` (default 10 seconds), and each later retry waits twice as long as the one before, up to `MaxAttemptDelay` (default 5 minutes). With the defaults a message is retried after 10, 20, 40 and 80 seconds.
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

## Queue statistics 📊

`IQueueStatisticsService.Read()` returns a snapshot for health checks and dashboards:

- `Available`: messages due now and waiting for a processor
- `Waiting`: messages not yet due (delayed, waiting to retry, or being processed)
- `OldestAvailableAgeSeconds`: how long the longest-waiting due message has been due. If this keeps growing, processors are stuck or falling behind.
- `Poisoned` and `Schedules`: row counts
