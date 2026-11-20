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

To produce your message you'll need to inject a `IProducerService`

## Immediate messages
You can produce messages to be consumed immediately like this:
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

The message is added to your `DbContext` and written when you call `SaveChangesAsync`, in the same transaction as your other changes. If you never save, or the save fails, no message is written. This works the same with every database package. Learn more about the [Transactional Outbox](../transactional-outbox) pattern.

## Delayed messages
You can produce messages to be consumed after a delay by passing a `TimeSpan`, or at a set time by passing a `DateTimeOffset`. Times are stored in whole seconds and rounded up, so a message is never consumed early.
```csharp

    order.Cancel();
    _dbContext.Orders.Update(order);
    await _producerService.Produce(new OrderCancelled()
    {
      OrderId = order.Id,
      CancelledAt = _timeProvider.UtcNow()
    }, TimeSpan.FromSeconds(60));

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);
```

## Deduplicated messages
Deduplicated messages are useful when you may emit the same message multiple times but only require it to be processed once for a given time period. For instance you may want to aggregate page views no more frequently then once every 10 seconds, you could schedule a reccuring message for this, but it may be wasteful if you anticipate pages go without views for extended periods of time.

```csharp

    pageView.Increment();
    _dbContext.PageViews.Update(pageView);
    await _producerService.Produce(new PageViewed()
    {
      PageId = pageView.Id,
    }, TimeSpan.FromSeconds(10), $"page_id:{pageView.Id}");

    // Save changes
    await _dbContext.SaveChangesAsync(cancellationToken);
```

How deduplication works (the key is stored in the `insert_id` column):

- **While pending only.** If a consumer already has a pending message with this deduplication key (saved, or produced on the same `DbContext` and not yet saved), that consumer is skipped. Once its message has been consumed or poisoned, the same key is accepted again. It is not a permanent idempotency key.
- **Per consumer.** For a payload with several consumers, only the consumers that already have the key pending are skipped.
- **Concurrent producers.** Two requests producing the same deduplication key at the same moment, in separate transactions, can both pass the check. The unique index on `(insert_id, consumer_type)` then stops the second, and its `SaveChangesAsync` throws `DbUpdateException`.

Summary

- **Transactional Persistence**: Produce messages along with changes to your `DbContext` before calling `SaveChangesAsync`, ensuring your domain changes and the messages they produce are persisted transactionally. Nothing is written until you save.
- **Delay**: Pass a `TimeSpan` delay or a `DateTimeOffset` to control when a message is consumed.
- **Deduplication**: By passing a `deduplicationKey`, a consumer that already has a pending message with that key is skipped. It is stored in the `insert_id` column. This is useful when you need a process to take place an amount of time after the first action in a sequence occurred.
