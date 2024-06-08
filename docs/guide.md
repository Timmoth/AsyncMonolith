# Message Handling Guide

## Producing Messages 📨

- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Deduplication**: By specifying a `insert_id` when producing messages the system ensures only one message with the same `insert_id` and `consumer_type` will be in the table at a given time. This is useful when you need a process to take place an amount of time after the first action in a sequence occured.

### Ef Example

The produce method when using pure Ef code will just add the messages directly to your DB context, calling `SaveChangesAsync` will ensure that the messages are inserted in the same transaction as your other domain updates.

```csharp
// Publish 'UserDeleted' to be processed in 60 seconds
  await _producerService.Produce(new UserDeleted()
  {
    Id = id
  }, 60);
await _dbContext.SaveChangesAsync(cancellationToken);
```

### MySql / PostgreSql Example

The produce method when using MySql or PostgreSQL makes use of `ExecuteSqlRawAsync`, if you want the messages to be inserted transactionally with your domain changes you must wrap all the changes in an explicit transaction.

```csharp
  await using var dbContextTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

	...

	// Publish 'UserDeleted' to be processed in 60 seconds
  await _producerService.Produce(new UserDeleted()
  {
    Id = id
  }, 60);

await _dbContext.SaveChangesAsync(cancellationToken);
await dbContextTransaction.CommitAsync(stoppingToken);

```

## Scheduling Messages ⌛

- **Frequency**: Scheduled messages will be produced periodically by the `chron_expression` in the given `chron_timezone`
- **Transactional Persistence**: Schedule messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Processing**: Schedule messages will be processed sequentially after they are made available by their chron job, at which point they will be turned into Consumer Messages and inserted into the `consumer_messages` table to be handled by their respective consumers.

  Example

  ```csharp
  // Publish 'CacheRefreshScheduled' every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
  _scheduledMessageService.Schedule(new CacheRefreshScheduled
    {
        Id = id
    }, "0 0 12 * * MON", "UTC", "id:{id}");
  await _dbContext.SaveChangesAsync(cancellationToken);
  ```

## Consuming Messages 📫

- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for a batch of available messages to process.
  - The query takes place at the frequency defined by `ProcessorMaxDelay`, if a full batch is returned it will delay by `ProcessorMinDelay`.
- **Concurrency**: Each app instance can run multiple parallel consumer processors defined by `ConsumerMessageProcessorCount`, unless using `AsyncMonolith.Ef`.
- **Batching**: Consumer messages will be read from the `consumer_messages` table in batches defined by `ConsumerMessageBatchSize`.
- **Idempotency**: Ensure your Consumers are idempotent, since they will be retried on failure.

Example

```csharp
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

## Changing Consumer Payload Schema 🔀

- **Backwards Compatibility**: When modifying consumer payload schemas, ensure changes are backwards compatible so that existing messages with the old schema can still be processed.
- **Schema Migration**:
  - If changes are not backwards compatible, make the changes in a copy of the `ConsumerPayload` (with a different class name) and update all consumers to operate on the new payload.
  - Once all messages with the old payload schema have been processed, you can safely delete the old payload schema and its associated consumers.

## Consumer Failures 💢

- **Retry Logic**: Messages will be retried up to `MaxAttempts` times (with a `AttemptDelay` seconds between attempts) until they are moved to the `poisoned_messages` table.
- **Manual Intervention**: If a message is moved to the `poisoned_messages` table, it will need to be manually removed from the database or moved back to the `consumer_messages` table to be retried. Note that the poisoned message will only be retried a single time unless you set `attempts` back to 0.
- **Monitoring**: Periodically monitor the `poisoned_messages` table to ensure there are not too many failed messages.

## OpenTelemetry Support 📊

Ensure you add `AsyncMonolithInstrumentation.ActivitySourceName` as a source to your OpenTelemetry configuration if you want to receive consumer / scheduled processor traces.

```csharp
        builder.Services.AddOpenTelemetry()
            .WithTracing(x =>
            {
                if (builder.Environment.IsDevelopment()) x.SetSampler<AlwaysOnSampler>();

                x.AddSource(AsyncMonolithInstrumentation.ActivitySourceName);
                x.AddConsoleExporter();
            })
            .ConfigureResource(c => c.AddService("async_monolith.demo").Build());
```
