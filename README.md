# AsyncMonolith ![Logo](AsyncMonolith/logo.png)
[![NuGet](https://img.shields.io/nuget/v/AsyncMonolith)](https://www.nuget.org/packages/AsyncMonolith)

AsyncMonolith is a lightweight library that facilitates simple asynchronous messaging in dotnet apps.

## Overview
✅ Makes building event driven architectures simple  
✅ Produce messages transactionally along with changes to your domain  
✅ Messages are stored in your DB context so you have full control over them  
✅ Supports running multiple instances / versions of your application  
✅ Delay processing messages  
✅ Schedule messages to be processed using Chron expressions  
✅ Deduplication ensures tagged messages are only processed once for time period  
✅ Automatic message retries  
✅ Automatically routes messages to multiple consumers  
✅ Keep your infrastructure simple, It only requires a dotnet API and database  
✅ Makes it very easy to write unit / integration tests  

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

## Support the project 🤝

- **🌟 Star this repository**: It means a lot to me and helps with exposure.
- **🪲 Report bugs**: Report any bugs you find by creating an issue.
- **📝 Contribute**: Read the [contribution guide](https://timmoth.github.io/AsyncMonolith/contributing) then pick up or create an issue.
