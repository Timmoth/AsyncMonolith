# AsyncMonolith

AsyncMonolith is a lightweight dotnet library that facillitates simple asynchronous processes in monolithic dotnet apps.

Setup

```csharp

    // Add Db Sets
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
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

    // Produce messages
    private readonly ProducerService<ApplicationDbContext> _producerService;

    _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    });

    await _dbContext.SaveChangesAsync(cancellationToken);
```

# Message Handling Guide

## Producing Messages

- **Save Changes**: Ensure that you call `SaveChangesAsync` after producing a message, unless you are producing a message inside a consumer, where it is called automatically.
- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring messages and your domain changes are persisted transactionally.

## Scheduling Messages

- **Delay**: Scheduled messages will be produced repeatedly at the frequency of the delay provided
- **Save Changes**: Ensure that you call `SaveChangesAsync` after creating a scheduled message, unless you are producing a message inside a consumer, where it is called automatically.
- **Transactional Persistence**: Schedule messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring messages and your domain changes are persisted transactionally.
- 
## Consuming Messages

- **Independent Consumption**: Each message will be consumed independently by each consumer set up to handle it.
- **Periodic Querying**: Each instance of your app will periodically query the `consumer_messages` table for available messages to process.
  - The query takes place every second and incrementally speeds up for every consecutive message processed.
  - Once there are no messages left, it will return to sampling the table every second.
- **Automatic Save Changes**: Each consumer will call `SaveChangesAsync` automatically after the abstract `Consume` method returns.

## Changing Consumer Payload Schema

- **Backwards Compatibility**: When modifying consumer payload schemas, ensure changes are backwards compatible so that existing messages with the old schema can still be processed.
- **Schema Migration**:
  - If changes are not backwards compatible, make the changes in a copy of the `ConsumerPayload` (with a different class name) and update all consumers to operate on the new payload.
  - Once all messages with the old payload schema have been processed, you can safely delete the old payload schema and its associated consumers.

## Consumer Failures

- **Retry Logic**: Messages will be processed up to 5 times (with a delay between attempts) until they are no longer attempted.
- **Manual Intervention**: If a message fails 5 times, it will need to be manually removed from the database or the `attempts` column reset for it to be retried.
- **Monitoring**: Periodically monitor the consumer table to ensure there are not too many failed messages.
