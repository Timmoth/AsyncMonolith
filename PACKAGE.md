# AsyncMonolith

Background messaging and scheduling for .NET monoliths, using the database you already have through Entity Framework Core.

- **Transactional outbox**: messages are written in the same `SaveChanges` as your data, so they are never lost or sent for changes that rolled back.
- **Consumers** run in the background with timeouts, retries with exponential backoff, and poisoned message handling.
- **Schedules**: recurring messages from cron expressions in any time zone.
- **Safe with several app instances**, with metrics on the `async_monolith` meter.

## Databases

One package works with any EF Core provider and picks its queries from the one you use:

| Provider | Row locking |
|---|---|
| PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`) | `FOR UPDATE SKIP LOCKED` |
| SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) | `UPDLOCK, READPAST` |
| MySQL / MariaDB (`MySql.EntityFrameworkCore` or Pomelo) | `FOR UPDATE SKIP LOCKED` |
| Anything else, such as SQLite | None: fine for a single instance |

`AsyncMonolith.TestHelpers` adds fakes and helpers for testing consumers with xunit v3.

## Quick start

```csharp
builder.Services.AddAsyncMonolith<AppDbContext>(o => o.RegisterTypesFromAssemblyContaining<Program>());

// In AppDbContext.OnModelCreating
modelBuilder.ConfigureAsyncMonolith();

public record OrderPlaced(int OrderId) : IConsumerPayload;

public class SendReceipt : IConsumer<OrderPlaced>
{
    public Task Consume(OrderPlaced payload, ConsumeContext context, CancellationToken cancellationToken) => ...;
}

await producer.Produce(new OrderPlaced(order.Id));
await dbContext.SaveChangesAsync();
```

Documentation: https://timmoth.github.io/AsyncMonolith/
