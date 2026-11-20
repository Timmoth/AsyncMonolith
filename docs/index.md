# [AsyncMonolith](https://github.com/Timmoth/AsyncMonolith) ![Logo](assets/logo.png)

[![NuGet](https://img.shields.io/nuget/v/AsyncMonolith?label=AsyncMonolith)](https://www.nuget.org/packages/AsyncMonolith)

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

!!! note

    Despite its name, AsyncMonolith can be used within a microservices architecture. The only requirement is that the producers and consumers share the same database i.e messaging within a single project. However, it is not suitable for passing messages between different projects in a microservices architecture, as microservices should not share the same database. 

# Support 🛟

Need help? Ping me on [linkedin](https://www.linkedin.com/in/timmoth/) and I'd be more then happy to jump on a call to debug, help configure or answer any questions.
