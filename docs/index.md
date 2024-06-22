# [AsyncMonolith](https://github.com/Timmoth/AsyncMonolith) ![Logo](assets/logo.png)

[![Ef](https://img.shields.io/nuget/v/AsyncMonolith.Ef?label=Ef)](https://www.nuget.org/packages/AsyncMonolith.Ef)
[![MySql](https://img.shields.io/nuget/v/AsyncMonolith.MySql?label=MySql)](https://www.nuget.org/packages/AsyncMonolith.MySql)
[![MsSql](https://img.shields.io/nuget/v/AsyncMonolith.MsSql?label=MsSql)](https://www.nuget.org/packages/AsyncMonolith.MsSql)
[![PostgreSql](https://img.shields.io/nuget/v/AsyncMonolith.PostgreSql?label=PostgreSql)](https://www.nuget.org/packages/AsyncMonolith.PostgreSql)
[![MariaDb](https://img.shields.io/nuget/v/AsyncMonolith.MariaDb?label=MariaDb)](https://www.nuget.org/packages/AsyncMonolith.MariaDb)

AsyncMonolith is a lightweight library that facilitates simple asynchronous messaging in dotnet apps.

## Overview
- Makes building event driven architectures simple  
- Produce messages transactionally along with changes to your domain  
- Messages are stored in your DB context so you have full control over them  
- Supports running multiple instances / versions of your application  
- Delay processing messages  
- Schedule messages to be processed using Chron expressions  
- Deduplication ensures tagged messages are only processed once for time period  
- Automatic message retries  
- Automatically routes messages to multiple consumers  
- Keep your infrastructure simple, It only requires a dotnet API and database  
- Makes it very easy to write unit / integration tests  

> [!NOTE]  
> Despite its name, AsyncMonolith can be used within a microservices architecture. The only requirement is that the producers and consumers share the same database i.e messaging within a single project. However, it is not suitable for passing messages between different projects in a microservices architecture, as microservices should not share the same database. 

!!! note

    Despite its name, AsyncMonolith can be used within a microservices architecture. The only requirement is that the producers and consumers share the same database i.e messaging within a single project. However, it is not suitable for passing messages between different projects in a microservices architecture, as microservices should not share the same database. 

# Support 🛟

Need help? Ping me on [linkedin](https://www.linkedin.com/in/timmoth/) and I'd be more then happy to jump on a call to debug, help configure or answer any questions.
