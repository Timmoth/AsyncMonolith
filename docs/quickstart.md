[For a more detailed example look at the Demo project](https://github.com/Timmoth/AsyncMonolith/tree/main/Demo)

Install the nuget package to support your database

[![Ef](https://img.shields.io/nuget/v/AsyncMonolith.Ef?label=Ef)](https://www.nuget.org/packages/AsyncMonolith.Ef)
[![MySql](https://img.shields.io/nuget/v/AsyncMonolith.MySql?label=MySql)](https://www.nuget.org/packages/AsyncMonolith.MySql)
[![MsSql](https://img.shields.io/nuget/v/AsyncMonolith.MsSql?label=MsSql)](https://www.nuget.org/packages/AsyncMonolith.MsSql)
[![PostgreSql](https://img.shields.io/nuget/v/AsyncMonolith.PostgreSql?label=PostgreSql)](https://www.nuget.org/packages/AsyncMonolith.PostgreSql)

```csharp
    // Pick one
    dotnet add package AsyncMonolith.Ef
    dotnet add package AsyncMonolith.MySql
    dotnet add package AsyncMonolith.MsSql
    dotnet add package AsyncMonolith.PostgreSql
```

Setup your DbContext

```csharp

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Add Db Sets
        public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
        public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
        public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;

        // Configure the ModelBuilder
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.ConfigureAsyncMonolith();
			base.OnModelCreating(modelBuilder);
		}
    }
```

***If you're not using ef migrations check out the sql to configure your database [here](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas)***

Register your dependencies

```csharp

    // Register required services
    builder.Services.AddLogging();
    builder.Services.AddSingleton(TimeProvider.System);

	// Register AsyncMonolith using either:
	// services.AddEfAsyncMonolith
	// services.AddMsSqlAsyncMonolith
	// services.AddMySqlAsyncMonolith
	// services.AddPostgreSqlAsyncMonolith

    builder.Services.AddPostgreSqlAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(), new AsyncMonolithSettings()
    {
        AttemptDelay = 10, // Seconds before a failed message is retried
        MaxAttempts = 5, // Number of times a failed message is retried
        ProcessorMinDelay = 10, // Minimum millisecond delay before the next batch is processed
        ProcessorMaxDelay = 1000, // Maximum millisecond delay before the next batch is processed
		ProcessorBatchSize = 5, // The number of messages to process in a single batch
        ConsumerMessageProcessorCount = 2, // The number of concurrent consumer message processors to run in each app instance
        ScheduledMessageProcessorCount = 1, // The number of concurrent scheduled message processors to run in each app instance
        DefaultConsumerTimeout = 10 // The default number of seconds before a consumer will timeout
    });
```

Create messages and consumers

```csharp

    // Define Consumer Payload
    public class ValueSubmitted : IConsumerPayload
    {
        [JsonPropertyName("value")]
        public required double Value { get; set; }
    }

    // Define Consumer
    [ConsumerTimeout(5)] // Consumer timeouts after 5 seconds
    public class ValueSubmittedConsumer : BaseConsumer<ValueSubmitted>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IProducerService _producerService;

        public ValueSubmittedConsumer(ApplicationDbContext dbContext, IProducerService producerService)
        {
            _dbContext = dbContext;
            _producerService = producerService;
        }

        public override Task Consume(ValueSubmitted message, CancellationToken cancellationToken)
        {
            ...
	        await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
```

Produce / schedule messages

```csharp

    // Inject services
    private readonly IProducerService _producerService;
    private readonly IScheduleService _scheduleService;

    // Produce a message to be processed immediately
    await _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    });

    // Produce a message to be processed in 10 seconds
    await _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    }, 10);

    // Produce a message to be processed in 10 seconds, but only once for a given userId
    await _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    }, 10, $"user:{userId}");

    // Publish a message every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
    _scheduleService.Schedule(new ValueSubmitted
    {
      Value = newValue
    }, "0 0 12 * * MON", "UTC", "id:{id}");

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);

```
