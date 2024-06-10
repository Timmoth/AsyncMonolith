Idempotency refers to the ability of your code to be executed multiple times whilst still yielding the same outcome. It is a critical property of handlers in an event driven system, since events can be reprocessed due to retries, network issues, or other failures. Without idempotency, reprocessing the same event could lead to inconsistent states, duplicate entries, or other unintended side effects.

Let's consider a simple scenario where an event handler updates the status of an order.

```csharp
order.Status = "cancelled";
await _dbContext.SaveChangesAsync();
await _emailService.SendCancellationEmail(order.Id);
```

The issue with the above code is that if the event is processed twice the `EmailService` will send a duplicate email.

We can achieve idempotency easily in this scenario by returning early if the order has already been cancelled.

```csharp
if(order.Status == "cancelled"){
    return;
}
order.Status = "cancelled";
await _dbContext.SaveChangesAsync();
await _emailService.SendCancellationEmail(order.Id);
```

You may notice an additional issue with the above example. If the `EmailService` throws an exception, the message will be retried but will exit early since the `Status` has already been set to `cancelled`. This problem can be solved by using [AsyncMonolith](https://github.com/Timmoth/AsyncMonolith) to emit an `OrderCancelled` event transactionally along with the changes to your domain, which subsequently invokes a handler that sends an email. Check out the post on the [Transactional Outbox](../transactional-outbox) pattern to learn more.

The revised version using AsyncMonolith may look like this:

```csharp
if(order.Status == "cancelled"){
    return;
}
order.Status = "cancelled";
await _producerService.Produce(new OrderCancelled()
  {
    OrderId = order.Id
  });
await _dbContext.SaveChangesAsync();
```

```csharp
public class SendOrderCancelledEmail : BaseConsumer<OrderCancelled>
{
    public override Task Consume(OrderCancelled message, CancellationToken cancellationToken)
    {
        ...
    }
}
```
