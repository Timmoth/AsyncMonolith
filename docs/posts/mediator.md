Let's consider a scenario where you are developing an ordering system. When a user cancels an order, you need to cancel the associated shipment and send the user an email confirming the cancellation.

```csharp
order.Cancel();
_dbContext.Orders.Update(order);
await _dbContext.SaveChangesAsync();
_shipmentService.CancelShipment(order.ShipmentId);
_emailService.SendCancellationEmail(order.Id);
```

The problem with this approach is that each service is tightly coupled with its dependencies. The `OrderService` must know to tell `ShipmentService` and the `EmailService` that an order has been cancelled. In a simple system, this might not cause significant problems, but as it grows, the number of connections between classes can make them difficult to maintain.

The Mediator pattern aims to solve this problem by introducing a `Mediator` service which acts as a coordinator between services. Each service sends requests to the Mediator without needing to concern itself with the responsibilities of other services.

After refactoring the `OrderService`, it now simply updates the order domain object and tells the Mediator that an order has been canceled:

```csharp
order.Cancel();
_dbContext.Orders.Update(order);
await _dbContext.SaveChangesAsync();
_mediator.Send(new OrderCancelled(order.Id));
```

It's the Mediator's job to route the request to each handler configured to handle the `OrderCancelled` event within their own context.

For example the handlers in this scenario could be implemented like this:

```csharp
public class CancelShipmentHandler
    {
        public async Task Handle(OrderCancelled orderCancelled)
        {
            ...
            shipment.Cancel();
            _dbContext.Shipments.Update(shipment);
            await _dbContext.SaveChangesAsync();
        }
    }

public class CancellationEmailHandler
    {
        public async Task Handle(OrderCancelled orderCancelled)
        {
            ...
            _emailService.SendCancellationEmail(order.Id);
        }
    }
```

This pattern promotes:

### Code reuse

You may have multiple places where an order can be canceled. Now, you don't have to duplicate the logic to cancel the shipment, send an email, or coordinate those actions when an order is cancelled.

### Single responsibility principle

Each handler is responsible for handling the `OrderCancelled` event within its own context.

### Open/closed principle

Additional handlers can be easily added to extend the behavior of your system without modifying existing code.

### Testability

Handlers can be unit tested in isolation.

One issue with the mediator pattern is that a part of your system could fail without recourse. For instance, if the `CancelShipmentHandler` fails, your system could be left in an inconsistent state since the order has been canceled, the email has been sent but shipment will still be made.

[AsyncMonolith](https://github.com/Timmoth/AsyncMonolith) acts as a mediator, providing the benefits of decoupling while ensuring transactional consistency by using the [Transactional Outbox](../transactional-outbox) pattern. This ensures that each message is stored in your database before being handled, so if anything fails, it will be retried multiple times before being moved into a `poisoned_messages` table where you can manually intervene.

Refactoring the above scenario to use AsyncMonolith may look like this:

```csharp
order.Cancel();
_dbContext.Orders.Update(order);
await _producerService.Produce(new OrderCancelled()
  {
    OrderId = order.Id
  });
await _dbContext.SaveChangesAsync();
```

```csharp
public class CancelShipment : BaseConsumer<OrderCancelled>
{
    public override Task Consume(OrderCancelled message, CancellationToken cancellationToken)
    {
        ...
        shipment.Cancel();
        _dbContext.Shipments.Update(shipment);
        await _dbContext.SaveChangesAsync();
    }
}

public class SendOrderCancelledEmail : BaseConsumer<OrderCancelled>
{
    public override Task Consume(OrderCancelled message, CancellationToken cancellationToken)
    {
        ...
        _emailService.SendCancellationEmail(order.Id);
    }
}
```
