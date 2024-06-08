(for a more detailed example look at the Demo project)

```csharp

    // Install the core package
    dotnet add package AsyncMonolith
	// Install the Db specific package
    dotnet add package AsyncMonolith.Ef
    dotnet add package AsyncMonolith.MySql
    dotnet add package AsyncMonolith.PostgreSql

    // Add Db Sets, and configure ModelBuilder
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
        public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
        public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.ConfigureAsyncMonolith();
			base.OnModelCreating(modelBuilder);
		}
    }

    // Register required services
    builder.Services.AddLogging();
    builder.Services.AddSingleton(TimeProvider.System);

	// Register AsyncMonolith using either:
	// services.AddEfAsyncMonolith
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
    });

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
	    await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // Produce / schedule messages
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly ScheduledMessageService<ApplicationDbContext> _scheduledMessageService;

    await _producerService.Produce(new ValueSubmitted()
    {
      Value = newValue
    });

    _scheduledMessageService.Schedule(new ValueSubmitted
    {
        Value = Random.Shared.NextDouble() * 100
    }, "*/5 * * * * *", "UTC");

    await _dbContext.SaveChangesAsync(cancellationToken);

```
