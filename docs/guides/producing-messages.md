To produce messages in dotnet apps using [AsyncMonolith](https://github.com/timmoth/asyncmonolith) you must first define a consumer payload class. This will act as the body of the message being passed to each consumer configured to handle it.

```csharp

public class OrderCancelled : IConsumerPayload
{
  [JsonPropertyName("order_id")]
  public string OrderId { get; set; }

  [JsonPropertyName("cancelled_at")]
  public DateTimeOffset CancelledAt { get; set; }
}

```

When defining a consumer payload it must derive from the `IConsumerPayload` interface and be serializable by the `System.Text.Json.JsonSerializer`.

As the consumer payload will be stored in the database in a serialized string, it is a good practice to keep it as small as possible.

To produce your message you'll need to inject a `ProducerService<ApplicationDbContext>`

## Immidiate messages
You can produce messages to be consumed immidiately like this:
```csharp

    order.Cancel();
    _dbContext.Orders.Update(order);
    await _producerService.Produce(new OrderCancelled()
    {
      OrderId = order.Id,
      CancelledAt = _timeProvider.UtcNow()
    });

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);
```

The message will be produced transactionally along with the change to your domain objects when you call `SaveChangesAsync`. Lean more about the [Transactional Outbox](../transactional-outbox) pattern.

***If using the MySql or PostgreSQL packages you will need to wrap your changes in a transaction see below***

## Delayed messages
You can produce messages to be consumed after a delay by specifying the number of seconds to wait before a consumer should process the message.
```csharp

    order.Cancel();
    _dbContext.Orders.Update(order);
    await _producerService.Produce(new OrderCancelled()
    {
      OrderId = order.Id,
      CancelledAt = _timeProvider.UtcNow()
    }, 60);

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);
```

## Deduplicated messages
Deduplicated messages are useful when you may emit the same message multiple times but only require it to be processed once for a given time period. For instance you may want to aggregate page views no more frequently then once every 10 seconds, you could schedule a reccuring message for this, but it may be wasteful if you anticipate pages go without views for extened periods of time.

```csharp

    pageView.Increment();
    _dbContext.PageViews.Update(pageView);
    await _producerService.Produce(new PageViewed()
    {
      PageId = pageView.Id,
    }, 10, $"page_id:{pageView.Id}");

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);
```

Deduplicated events will ensure only a single message for a given consumer type and insertId are ever pending processing at any given time.

### MySql / PostgreSql Transactionality

The produce method makes use of `ExecuteSqlRawAsync` when using the MySql or PostgreSQL package, if you want the messages to be inserted transactionally with your domain changes you must wrap all the changes in an explicit transaction.

```csharp
  await using var dbContextTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

  order.Cancel();
  _dbContext.Orders.Update(order);
  await _producerService.Produce(new OrderCancelled()
  {
    OrderId = order.Id,
    CancelledAt = _timeProvider.UtcNow()
  });

  await _dbContext.SaveChangesAsync(cancellationToken);
  await dbContextTransaction.CommitAsync(cancellationToken);

```

Summary

- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally.
- **Delay**: Specify the number of seconds a message should remain in the queue before it is processed by a consumer.
- **Deduplication**: By specifying a `insert_id` when producing messages the system ensures only one message with the same `insert_id` and `consumer_type` will be in the table at a given time. This is useful when you need a process to take place an amount of time after the first action in a sequence occured.
