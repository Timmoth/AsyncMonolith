Consider a scenario where when a user places an order, you also need to create an entity that tracks the shipment.

This scenario is fine as the order and shipment are both committed to your database transactionally; either both succeed, or neither do. You will never have an order created without a shipment.

```csharp
_dbContext.Orders.Add(newOrder);
_dbContext.Shipments.Add(newShipment);
await _dbContext.SaveChangesAsync();
```

As your application grows, you find that an increasing number of things need to happen when an order is created, and some of them have their own chain of operations. This will quickly cause any method that needs to create an order to grow in complexity and scope. As a result you decide you want to decouple the process of creating an order from the process of creating a shipment, so you adopt an event-driven approach.

However, now you have to make a difficult decision: should I commit my order to my database first, then produce an event, or should I produce the event first, then commit the order to my database?

In the first scenario, it is possible an order is created but publishing the 'OrderCreated' event fails.

```csharp
// Create order
_dbContext.Orders.Add(newOrder);
await _dbContext.SaveChangesAsync();
// Dispatch event
channel.BasicPublish(...); \\ This fails
```

In the second scenario, it is possible that an event is published, but the order fails to be created.

```csharp
// Dispatch event
channel.BasicPublish(...);
// Create order
_dbContext.Orders.Add(newOrder);
await _dbContext.SaveChangesAsync(); \\ This fails
```

The transactional outbox is a pattern which solves this problem. The general idea is that instead of publishing an event along with making a change to your database, you create an entity that will publish an event at a later time and commit both changes to the database as part of the same transaction. You then have a process capable of checking for records in the outbox table and publishing them as events while handling failures and retries.

```csharp
// Create order
_dbContext.Orders.Add(newOrder);
// Insert event into outbox
_dbContext.Outbox.Add(orderCreated)
await _dbContext.SaveChangesAsync();
```

For simple use cases, you may realize that the outbox message can act as the event, and you don't actually need the additional step of publishing to a message broker. Instead, you write your events to your database along with the changes to your domain and have a process that periodically fetches, processes, and then removes messages from the outbox table. This is one of the core principles behind [AsyncMonolith](https://github.com/Timmoth/AsyncMonolith).

In summary, the transactional outbox pattern ensures that events are reliably published, maintaining consistency between operations. While it introduces the need for an additional process to handle the outbox and increases the load on your application database, the benefits of decoupling and reliability often outweigh these overheads.

Here is how the above example may look if you’re using [AsyncMonolith](https://github.com/Timmoth/AsyncMonolith)

```csharp
    // Create order
    _dbContext.Orders.Add(newOrder);
    // Insert event into outbox
    await _producerService.Produce(new OrderCreated()
    {
        OrderId = order.Id
    });
    await _dbContext.SaveChangesAsync();
```

```csharp
    public class CreateShipment : BaseConsumer<OrderCreated>
    {
        public override Task Consume(OrderCreated message, CancellationToken cancellationToken)
        {
            ...
            // Create Order
        }
    }
```
