# AsyncMonolith ![Logo](AsyncMonolith/logo.png)
[![Ef](https://img.shields.io/nuget/v/AsyncMonolith.Ef?label=Ef)](https://www.nuget.org/packages/AsyncMonolith.Ef)
[![MySql](https://img.shields.io/nuget/v/AsyncMonolith.MySql?label=MySql)](https://www.nuget.org/packages/AsyncMonolith.MySql)
[![MsSql](https://img.shields.io/nuget/v/AsyncMonolith.MsSql?label=MsSql)](https://www.nuget.org/packages/AsyncMonolith.MsSql)
[![PostgreSql](https://img.shields.io/nuget/v/AsyncMonolith.PostgreSql?label=PostgreSql)](https://www.nuget.org/packages/AsyncMonolith.PostgreSql)
[![MariaDb](https://img.shields.io/nuget/v/AsyncMonolith.MariaDb?label=MariaDb)](https://www.nuget.org/packages/AsyncMonolith.MariaDb)

AsyncMonolith is a lightweight library that facilitates simple, durable and asynchronous messaging in dotnet apps.

### Overview:
- Speed up your API by offloading tasks to a background worker.
- Distribute workload amongst multiple app instances.
- Execute tasks at specific times or after a delay.
- Execute tasks at regular intervals.
- Simplify complex processes by building out an event-driven architecture.
- Decouple your services by utilizing the Mediator pattern.
- Improve your application's resiliency by utilizing the Transactional Outbox pattern.
- Improve your application's resiliency by utilizing automatic retries.
- Keep your infrastructure simple without using a message broker.
- Simplify testability.


> [!NOTE]  
> Despite its name, AsyncMonolith can be used within a microservices architecture. The only requirement is that the producers and consumers share the same database i.e messaging within a single project. However, it is not suitable for passing messages between different projects in a microservices architecture, as microservices should not share the same database. 

## Find out more 🤔
  - [Overview ✅](https://timmoth.github.io/AsyncMonolith/index)
  - [Quick start ▶️](https://timmoth.github.io/AsyncMonolith/quickstart)
  - [Internals 🧠](https://timmoth.github.io/AsyncMonolith/internals)
  - [Releases 📒](https://timmoth.github.io/AsyncMonolith/releases)
  - [Warnings ⚠️](https://timmoth.github.io/AsyncMonolith/warnings)
  - [Support 🛟](https://timmoth.github.io/AsyncMonolith/support)
  - [Demo App ✨](https://timmoth.github.io/AsyncMonolith/demo)
  - [Contributing 🙏](https://timmoth.github.io/AsyncMonolith/contributing)
  - [Tests 🐞](https://timmoth.github.io/AsyncMonolith/tests)

Check out [this video](https://www.youtube.com/watch?v=DOaDpHh1FsQ) from Derek Comartin, for some thoughts on using a database as a queue

## Guides
  - [Producing messages](https://timmoth.github.io/AsyncMonolith/guides/producing-messages/)
  - [Scheduling messages](https://timmoth.github.io/AsyncMonolith/guides/scheduling-messages/)
  - [Consuming messages](https://timmoth.github.io/AsyncMonolith/guides/consuming-messages/)
  - [Changing messages](https://timmoth.github.io/AsyncMonolith/guides/changing-messages/)
  - [Open Telemetry](https://timmoth.github.io/AsyncMonolith/guides/opentelemetry/)

    
## Posts
  - [What is the Transactional Outbox?](https://timmoth.github.io/AsyncMonolith/posts/transactional-outbox/)
  - [What is the Mediator pattern?](https://timmoth.github.io/AsyncMonolith/posts/mediator/)
  - [What is Idempotency?](https://timmoth.github.io/AsyncMonolith/posts/idempotency/)

## Quick Demo
Producing and scheduling messages
```csharp
  // Publish 'UserDeleted' to be processed in 60 seconds
  await _producerService.Produce(new UserDeleted()
  {
    Id = id
  }, 60);
  
 // Publish 'CacheRefreshScheduled' every Monday at 12pm (UTC) with a tag that can be used to modify / delete related scheduled messages.
 _scheduleService.Schedule(new CacheRefreshScheduled
   {
       Id = id
   }, "0 0 12 * * MON", "UTC", "id:{id}");
 await _dbContext.SaveChangesAsync(cancellationToken);
```
Consuming messages
```csharp
[ConsumerTimeout(5)] // Consumer timeouts after 5 seconds
public class DeleteUsersPosts : BaseConsumer<UserDeleted>
{
    private readonly ApplicationDbContext _dbContext;

    public ValueSubmittedConsumer(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override Task Consume(UserDeleted message, CancellationToken cancellationToken)
    {
        ...
		await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```
## Collaboration 🙏
Like the idea and want to get involved? Check out the open issues or shoot me a message if you've got any ideas / feedback!

## Support 🛟
Need help? Ping me on [linkedin](https://www.linkedin.com/in/timmoth/) and I'd be more then happy to jump on a call to debug, help configure or answer any questions.

## Discord
Come say hello on the [Discord server](https://discord.gg/ZPqVWptK5B)

## Support the project 🤝

- **🌟 Star this repository**: It means a lot to me and helps with exposure.
- **🪲 Report bugs**: Report any bugs you find by creating an issue.
- **📝 Contribute**: Read the [contribution guide](https://timmoth.github.io/AsyncMonolith/contributing) then pick up or create an issue.
