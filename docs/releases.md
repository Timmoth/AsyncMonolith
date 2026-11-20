Make sure to check this table before updating the nuget package in your solution, you may be required to add an `dotnet ef migration`.

| Version | Description                                | Migration Required |
|---------|--------------------------------------------|--------------------|
| 11.0.0  | One package, multi-instance-safe processing, new consumer and options APIs (see below) | Yes |
| 8.0.6   | Added ConsumerAttempts override attribute  | No                 |
| 8.0.5   | Bundle debug symbols with nuget package    | No                 |
| 8.0.4   | Bundle XML docs with nuget package         | No                 |
| 8.0.3   | Optimised Sql                              | No                 |
| 8.0.2   | Added distributed trace_id and span_id     | No                 |
| 8.0.1   | Added OpenTelemetry support                | Yes                |
| 8.0.0   | Use Producer & Schedule service interfaces | No                 |
| 1.0.9   | Initial                                    | Yes                |

***If you're not using ef migrations check out the sql to configure your database [here](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas)***


## 11.0.0

AsyncMonolith is now a single package. From 11.0.0 the major version follows [Semantic Versioning](https://semver.org) (it changes for breaking changes) rather than the .NET version. It is tested against PostgreSQL, SQL Server, MySQL 8.4 (with Oracle's `MySql.EntityFrameworkCore`), MariaDB 11.8 and SQLite.

**Upgrading from 8.x or 10.x**

1. Replace `AsyncMonolith.Ef`, `.PostgreSql`, `.MsSql`, `.MySql` or `.MariaDb` with the `AsyncMonolith` package, and add your EF Core provider if your app doesn't reference it already.
2. Replace `services.Add{Database}AsyncMonolith<TDbContext>(settings => ...)` with `services.AddAsyncMonolith<TDbContext>(options => ...)`, converting times to `TimeSpan`s (see the options changes below).
3. Make consumers implement `IConsumer<TPayload>` and add the `ConsumeContext context` parameter to `Consume`.
4. Call `modelBuilder.ConfigureAsyncMonolith()` in `OnModelCreating`, then add and apply a migration (see below).
5. On PostgreSQL, SQL Server, MySQL or MariaDB, make sure every `Produce` is followed by `SaveChangesAsync`: messages are no longer written without it.

**Migration required.** Add a migration (`dotnet ef migrations add AsyncMonolith_11_0_0`). It adds a nullable `last_error` column to `consumer_messages` and `poisoned_messages`. It also adds indexes for finding due messages (`rail_id, available_after`), due schedules (`available_after`), schedules by tag, and poisoned messages by age, which keep processing fast as the tables grow. If you manage the schema by hand, see the [SQL schemas](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas). They also now include `rail_id`, and their `trace_id` and `span_id` columns are nullable.

**Changes**
- Metrics on the `async_monolith` meter, and optional `PoisonedMessageRetention`.
- A schedule that can't be evaluated, or whose payload has no consumers, no longer stops the others. A consumer that ignores cancellation is abandoned two seconds after its timeout. Processors back off while the database is unavailable.
- Cron expressions can have 5 fields (minute precision) as well as 6 (seconds first).
- `AsyncMonolithOptions.SerializerOptions` sets the `JsonSerializerOptions` for payloads. The default is unchanged.
- Tables can live in their own schema or have a prefix: `modelBuilder.ConfigureAsyncMonolith(o => { o.Schema = "jobs"; o.TablePrefix = "jobs_"; })`. The row-locking SQL follows the mapping.
- Every database now produces messages the same way: they are added to your `DbContext` and written on `SaveChanges`, in your transaction. The PostgreSQL, SQL Server, MySQL and MariaDB packages used to write immediately. A repeated deduplication key skips consumers that already have a pending message with it, on every database.
- Several app instances can safely share the tables with any database. Claims and schedule occurrences use conditional updates, so a message is processed by one processor at a time and a schedule fires once per occurrence.
- Fetch order is deterministic (oldest first, then by id). See [Ordering](https://timmoth.github.io/AsyncMonolith/guides/consuming-messages/#ordering) for what is and isn't guaranteed.
- Consumers can write to the database on SQLite. Messages are claimed and leased in a short transaction, consumers run outside it, and each message is completed in its own short write. If the app stops mid-message, the message is retried once its consumer timeout plus 30 seconds has passed.
- Retries back off exponentially from `AttemptDelay` up to the new `MaxAttemptDelay` option (default 5 minutes).
- The last error is stored on retried and poisoned messages.
- `IPoisonedMessageService` counts, lists, retries and deletes poisoned messages.
- `IScheduleService.EnsureSchedule` creates or updates a schedule by tag, so it can run at every startup.
- `IQueueStatisticsService` reports queue depth and the age of the oldest due message, for health checks.
- `[ConsumerPayloadName]` and `[ConsumerName]` give payloads and consumers stable stored names. Name clashes now fail at startup instead of silently mixing messages.
- `AddAsyncMonolithConsumers` lets modules register their own consumers alongside the host's `AddAsyncMonolith` call.

**Breaking changes**
- **One package.** `AsyncMonolith.Ef`, `.PostgreSql`, `.MsSql`, `.MySql` and `.MariaDb` are replaced by `AsyncMonolith` alone, and `AddEfAsyncMonolith`/`AddPostgreSqlAsyncMonolith`/... by `AddAsyncMonolith<TDbContext>()`. The queries are chosen from your DbContext's EF Core provider, and AsyncMonolith no longer references any database driver: bring your own provider (for MySQL, `MySql.EntityFrameworkCore`; Pomelo works once it supports EF Core 10).
- `ConfigureAsyncMonolith()` now maps all three tables (previously only an index), so DbSet properties for them are optional. It takes an optional schema and table prefix. The `[Table]` attributes on the entities are removed; call `ConfigureAsyncMonolith()` in `OnModelCreating` if you weren't already.
- `IPoisonedMessageService.List` returns `PoisonedMessageInfo` records with `DateTimeOffset` times instead of `PoisonedMessage` entities.
- Implementation types are now internal: `ConsumerRegistry`, the processors and processor factory, the fetchers and their interfaces, `ScheduleService`, `PoisonedMessageService`, `QueueStatisticsService`, `AsyncMonolithIdGenerator`, `AsyncMonolithInstrumentation.ActivitySource` (the source name constants stay public), and `ScheduledMessage.GetNextOccurrence`/`UpdateSchedule`/`UpdatePayload`. Use the service interfaces instead. `FakeProducerService` is created by `AddFakeAsyncMonolithBaseServices`.
- `IProducerService.Produce` takes a `TimeSpan? delay` or a `DateTimeOffset availableAt` instead of a Unix-seconds `long availableAfter`, and `insertId` is renamed `deduplicationKey` (still stored in `insert_id`). `ProduceList` takes an `IEnumerable<T>`. `Produce(ScheduledMessage)` is no longer on the interface.
- Renamed: `ChronExpression`/`ChronTimezone` → `CronExpression`/`CronTimeZone` (columns unchanged), `InsertId` → `DeduplicationKey` (column unchanged), `ConsumerInstanceExecutionMode` → `ConsumerExecutionMode`, and `IScheduleService` parameters to `cronExpression`/`timeZone`. No migration is needed for these.
- `AsyncMonolithSettings` is now `AsyncMonolithOptions`, configured through the standard options pattern and validated when the host starts (an `OptionsValidationException` instead of an `ArgumentException` at registration). Times are `TimeSpan`s, and some names changed: `ProcessorBatchSize` → `BatchSize`, `ProcessorMaxDelay` (ms) → `IdlePollInterval`, `ProcessorMinDelay` (ms) → `BusyPollInterval`. `AsyncMonolithSettings.Default` is gone; use `new AsyncMonolithOptions()`.
- `AddAsyncMonolith` returns an `AsyncMonolithBuilder` (for `.BindConfiguration(...)` and `.AddConsumers(...)`) instead of `IServiceCollection`, and the obsolete overloads taking an `Assembly` are removed. The TestHelpers setup methods take `Action<AsyncMonolithOptions>`.
- `[ConsumerTimeout]`'s `Duration` property is now `Seconds`.
- Consumers implement `IConsumer<TPayload>` and receive a `ConsumeContext`; `BaseConsumer<T>` is removed.
- PostgreSQL, SQL Server, MySQL and MariaDB users: messages are no longer written until you call `SaveChanges`. If you produced messages without saving, call `SaveChangesAsync`.
- `AddAsyncMonolith` no longer registers `DbContext` as an alias for your context, and throws if called twice (modules use `AddAsyncMonolithConsumers`). Its services use `TryAdd`, so services you register first win.
- Removed public types: the per-database producer services and the unused `ScheduledMessageProcessorFactory`. Resolve `IProducerService` instead.
- `AsyncMonolith.TestHelpers` uses xunit v3, and `ConsumerTestBase.InitializeAsync`/`DisposeAsync` return `ValueTask`.
- `IScheduleService` has a new member (`EnsureSchedule`), so custom implementations must add it.
- Errors that were thrown as `System.Exception` are now `InvalidOperationException`.
- `TestConsumerMessageProcessor.ProcessNext<TDbContext, TConsumer>` now finds the consumer's message. It previously always returned null.
