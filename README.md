# AsyncMonolith ![Logo](AsyncMonolith/logo.png)
[![NuGet](https://img.shields.io/nuget/v/AsyncMonolith)](https://www.nuget.org/packages/AsyncMonolith)

AsyncMonolith is a lightweight dotnet library that facillitates simple asynchronous processes in monolithic dotnet apps.

# Overview

- Makes building event driven architectures simple
- Produce messages transactionally along with changes to your domain
- Messages are stored in your DB context so you have full control over them
- Supports running multiple instances / versions of your application
- Schedule messages to be processed using Chron expressions
- Automatic message retries
- Automatically routes messages to multiple consumers

# Warnings

Async Monolith is not a replacement for a message broker, there are many reasons why you may require one including:
- Extremely high message throughput (Async Monolith will tax your DB)
- Message ordering (Not currently supported)
- Communicating between different services (It's in the name)

I'd reccomend watching this [video](https://www.youtube.com/watch?v=DOaDpHh1FsQ) by Derik Comartin before deciding to use Async Monolith.

Efcore does not natively support row level locking, this makes it possible for two instances of your app to compete over the next available message to be processed, potentially wasting cycles.
Using `DbType.PostgreSql` or `DbType.MySql` will allow AsyncMonolith to lock rows ensuring they are only retrieved and processed once.

# Dev log

Make sure to check this table before updating the nuget package in your solution, you may be required to add an `dotnet ef migration`.
| Version      | Description | Requires Migration |
| ----------- | ----------- |----------- |
| 1.0.4      | Added poisoned message table   | Yes |
| 1.0.3      | Added mysql support   | Yes |
| 1.0.2      | Scheduled messages use Chron expressions   | Yes |
| 1.0.1      | Added Configurable settings    | No |
| 1.0.0      | Initial   | Yes |

# Message Handling Guide

## Producing Messages

- **Save Changes**: Ensure that you call `SaveChangesAsync` after producing a message, unless you are producing a message inside a consumer, where it is called automatically.
- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.

  Example
  ```csharp
  // Publish 'UserDeleted' to be processed in 60 seconds
    _producerService.Produce(new UserDeleted()
    {
      Id = id
    }, 60);
  await _dbContext.SaveChangesAsync(cancellationToken);
  ```
  
## Scheduling Messages

- **Frequency**: Scheduled messages will be produced repeatedly at the frequency defined by the given `chron_expression` in the given `chron_timezone`
- **Save Changes**: Ensure that you call `SaveChangesAsync` after creating a scheduled message, unless you are producing a message inside a consumer, where it is called automatically.
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
## Consuming Messages

- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for available messages to process.
  - The query takes place every second and incrementally speeds up for every consecutive message processed.
  - Once there are no messages left, it will return to sampling the table every second.
- **Automatic Save Changes**: Each consumer will call `SaveChangesAsync` automatically after the abstract `Consume` method returns.

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
    }
}
```
## Changing Consumer Payload Schema

- **Backwards Compatibility**: When modifying consumer payload schemas, ensure changes are backwards compatible so that existing messages with the old schema can still be processed.
- **Schema Migration**:
  - If changes are not backwards compatible, make the changes in a copy of the `ConsumerPayload` (with a different class name) and update all consumers to operate on the new payload.
  - Once all messages with the old payload schema have been processed, you can safely delete the old payload schema and its associated consumers.

## Consumer Failures

- **Retry Logic**: Messages will be retried up to `MaxAttempts` times (with a `AttemptDelay` seconds between attempts) until they are moved to the `poisoned_messages` table.
- **Manual Intervention**: If a message is moved to the `poisoned_messages` table, it will need to be manually removed from the database or moved back to the `consumer_messages` table to be retried. Note that the poisoned message will only be retried a single time unless you set `attempts` back to 0.
- **Monitoring**: Periodically monitor the `poisoned_messages` table to ensure there are not too many failed messages.

# Diagram
![Logo](Diagrams/AsyncMonolith.svg)

# Quick start guide 
(for a more detailed example look at the Demo project)

```csharp

    // Install AsyncMonolith
    dotnet add package AsyncMonolith

    // Add Db Sets
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
        public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
        public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;
    }

    // Register services
    builder.Services.AddLogging();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(), new AsyncMonolithSettings()
    {
        AttemptDelay = 10, // Seconds before a failed message is retried
        MaxAttempts = 5, // Number of times a failed message is retried 
        ProcessorMinDelay = 10, // Minimum millisecond delay before the next message is processed
        ProcessorMaxDelay = 1000, // Maximum millisecond delay before the next message is processed
        DbType = DbType.PostgreSql, // Type of database being used (use DbType.Ef if not supported)
    });
    builder.Services.AddControllers();

    // Define Consumer Payloads
    public class ValueSubmitted : IConsumerPayload
    {
        [JsonPropertyName("value")]
        public required double Value { get; set; }
    }

    // Define Consumers
    public class ValueSubmittedConsumer : BaseConsumer<ValueSubmitted>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ProducerService<ApplicationDbContext> _producerService;
    
        public ValueSubmittedConsumer(ApplicationDbContext dbContext, ProducerService<ApplicationDbContext> producerService)
        {
            _dbContext = dbContext;
            _producerService = producerService;
        }
    
        public override Task Consume(ValueSubmitted message, CancellationToken cancellationToken)
        {
            ...
        }
    }

    // Produce / schedule messages
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly ScheduledMessageService<ApplicationDbContext> _scheduledMessageService;

    _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    });

    _scheduledMessageService.Schedule(new ValueSubmitted
    {
        Value = Random.Shared.NextDouble() * 100
    }, "*/5 * * * * *", "UTC");
    await _dbContext.SaveChangesAsync(cancellationToken);

```
