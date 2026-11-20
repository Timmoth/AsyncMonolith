Make sure to check this table before updating the nuget package in your solution, you may be required to add an `dotnet ef migration`.

| Version | Description                                | Migration Required |
|---------|--------------------------------------------|--------------------|
| 11.0.0  | SQLite-safe processing, stored names, modules, backoff, poisoned message service (see below) | Yes |
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

All packages now share one version and must be upgraded together. From 11.0.0 the major version follows [Semantic Versioning](https://semver.org) (it changes for breaking changes) rather than the .NET version. `AsyncMonolith.MySql` and `AsyncMonolith.MariaDb` are published as previews (`11.0.0-preview.1`) until Pomelo supports EF Core 10 and they run in the integration tests.

**Migration required.** Add a migration (`dotnet ef migrations add AsyncMonolith_11_0_0`). It adds a nullable `last_error` column to `consumer_messages` and `poisoned_messages`. If you manage the schema by hand, see the [SQL schemas](https://github.com/Timmoth/AsyncMonolith/tree/main/Schemas). They also now include `rail_id`, and their `trace_id` and `span_id` columns are nullable.

**Changes**
- Every database package now produces messages the same way: they are added to your `DbContext` and written on `SaveChanges`, in your transaction. The PostgreSQL, SQL Server, MySQL and MariaDB packages used to write immediately. A repeated insert id skips consumers that already have a pending message with it, on every database.
- Several app instances can safely share the tables with any database package. Claims and schedule occurrences use conditional updates, so a message is processed by one processor at a time and a schedule fires once per occurrence.
- Fetch order is deterministic (oldest first, then by id). See [Ordering](guides/consuming-messages.md#ordering) for what is and isn't guaranteed.
- Consumers can write to the database on SQLite. Messages are claimed and leased in a short transaction, consumers run outside it, and each message is completed in its own short write. If the app stops mid-message, the message is retried once its consumer timeout plus 30 seconds has passed.
- Retries back off exponentially from `AttemptDelay` up to the new `MaxAttemptDelay` option (default 5 minutes).
- The last error is stored on retried and poisoned messages.
- `IPoisonedMessageService` counts, lists, retries and deletes poisoned messages.
- `IScheduleService.EnsureSchedule` creates or updates a schedule by tag, so it can run at every startup.
- `IQueueStatisticsService` reports queue depth and the age of the oldest due message, for health checks.
- `[ConsumerPayloadName]` and `[ConsumerName]` give payloads and consumers stable stored names. Name clashes now fail at startup instead of silently mixing messages.
- `AddAsyncMonolithConsumers` lets modules register their own consumers alongside the host's `Add*AsyncMonolith` call.

**Breaking changes**
- `AsyncMonolithSettings` is now `AsyncMonolithOptions`, configured through the standard options pattern and validated when the host starts (an `OptionsValidationException` instead of an `ArgumentException` at registration). Times are `TimeSpan`s, and some names changed: `ProcessorBatchSize` → `BatchSize`, `ProcessorMaxDelay` (ms) → `IdlePollInterval`, `ProcessorMinDelay` (ms) → `BusyPollInterval`. `AsyncMonolithSettings.Default` is gone; use `new AsyncMonolithOptions()`.
- `Add*AsyncMonolith` returns an `AsyncMonolithBuilder` (for `.BindConfiguration(...)` and `.AddConsumers(...)`) instead of `IServiceCollection`, and the obsolete overloads taking an `Assembly` are removed. The TestHelpers setup methods take `Action<AsyncMonolithOptions>`.
- `[ConsumerTimeout]`'s `Duration` property is now `Seconds`.
- Consumers implement `IConsumer<TPayload>` and receive a `ConsumeContext`; `BaseConsumer<T>` is removed.
- PostgreSQL, SQL Server, MySQL and MariaDB users: messages are no longer written until you call `SaveChanges`. If you produced messages without saving, call `SaveChangesAsync`.
- `Add*AsyncMonolith` no longer registers `DbContext` as an alias for your context, and throws if called twice (modules use `AddAsyncMonolithConsumers`). Its services use `TryAdd`, so services you register first win.
- Removed public types: `EfProducerService`, `PostgreSqlProducerService`, `MsSqlProducerService`, `MySqlProducerService`, `MariaDbProducerService` and the unused `ScheduledMessageProcessorFactory`. Resolve `IProducerService` instead.
- `AsyncMonolith.TestHelpers` uses xunit v3, and `ConsumerTestBase.InitializeAsync`/`DisposeAsync` return `ValueTask`.
- `IScheduleService` has a new member (`EnsureSchedule`), so custom implementations must add it.
- Errors that were thrown as `System.Exception` are now `InvalidOperationException`.
- `ConsumerRegistry` exposes properties instead of public fields.
- `TestConsumerMessageProcessor.ProcessNext<TDbContext, TConsumer>` now finds the consumer's message. It previously always returned null.
