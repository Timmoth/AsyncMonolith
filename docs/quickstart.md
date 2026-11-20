[For a more detailed example look at the Demo project](https://github.com/Timmoth/AsyncMonolith/tree/main/Demo)

Install the nuget package to support your database

[![NuGet](https://img.shields.io/nuget/v/AsyncMonolith?label=AsyncMonolith)](https://www.nuget.org/packages/AsyncMonolith)

```csharp
    dotnet add package AsyncMonolith
```

Setup your DbContext

```csharp

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Map AsyncMonolith's tables. DbSet properties for them are optional.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureAsyncMonolith();

            // Or keep them apart from your own tables:
            // modelBuilder.ConfigureAsyncMonolith(o =>
            // {
            //     o.Schema = "jobs";        // databases that support schemas
            //     o.TablePrefix = "jobs_";  // jobs_consumer_messages, ...
            // });

            base.OnModelCreating(modelBuilder);
        }
    }
```

***If you're not using ef migrations check out the sql to configure your database [here](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas)***

Register your dependencies and configure settings

```csharp

    // Your DbContext, with any EF Core provider
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));

    // AsyncMonolith picks its queries from the provider: PostgreSQL (Npgsql), SQL Server, and MySQL or MariaDB
    // (MySql.EntityFrameworkCore or Pomelo) lock rows so several app instances can share the tables. Any other
    // provider, such as SQLite, works without row locks (fine for a single instance).
    builder.Services.AddAsyncMonolith<ApplicationDbContext>(options =>
    {
        options.RegisterTypesFromAssemblyContaining<Program>();
        options.MaxAttempts = 5; // Attempts before a message is poisoned
        options.AttemptDelay = TimeSpan.FromSeconds(10); // First retry delay; doubles each retry
        options.MaxAttemptDelay = TimeSpan.FromMinutes(5); // Longest retry delay
        options.DefaultConsumerTimeout = TimeSpan.FromSeconds(10); // Before a consumer is cancelled
        options.BatchSize = 5; // Messages a processor takes at once
        options.IdlePollInterval = TimeSpan.FromSeconds(1); // Wait after a batch that wasn't full
        options.BusyPollInterval = TimeSpan.FromMilliseconds(10); // Wait after a full batch
    })
    .BindConfiguration("AsyncMonolith"); // Optional: override any option from appsettings.json
```

Options are validated when the host starts, so an invalid value stops startup with an `OptionsValidationException` that names it. In `appsettings.json`, write times as `"00:00:30"`.

The following methods are available on the `AsyncMonolithOptions` passed into the `options` lambda:

- `RegisterTypesFromAssemblyContaining<T>()`
- `RegisterTypesFromAssemblyContaining(Type type)`
- `RegisterTypesFromAssembly(Assembly assembly)`
- `RegisterTypesFromAssemblies(params Assembly[] assemblies)`

### Modules

Call `AddAsyncMonolith` once, in the host. A module or library with its own consumers registers them itself, before or after the host's call:

```csharp
services.AddAsyncMonolithConsumersFromAssemblyContaining<BillingModule>();
// or
services.AddAsyncMonolithConsumers(typeof(BillingModule).Assembly);
```

Registering an assembly more than once is harmless. Consumers from every module are checked together when the host starts: a payload with no consumer in any module, or two payloads or consumers with the same stored name, stops startup with an error (see [Stored names](guides/changing-messages.md#stored-names)).

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
    [ConsumerAttempts(1)] // Consumer messages moved to poisoned table after 1 failed attempt
    public class ValueSubmittedConsumer : IConsumer<ValueSubmitted>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IProducerService _producerService;

        public ValueSubmittedConsumer(ApplicationDbContext dbContext, IProducerService producerService)
        {
            _dbContext = dbContext;
            _producerService = producerService;
        }

        public Task Consume(ValueSubmitted message, ConsumeContext context, CancellationToken cancellationToken)
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
    }, TimeSpan.FromSeconds(10), $"user:{userId}");

    // Publish a message every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
    _scheduleService.Schedule(new ValueSubmitted
    {
      Value = newValue
    }, "0 0 12 * * MON", "UTC", "id:{id}");

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);

```
