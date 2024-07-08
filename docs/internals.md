![Logo](assets/internals.svg)

## ProducerService

Resolves consumers for a given payload and writes messages to the `consumer_messages` table for processing.

## ScheduleService

Writes scheduled messages to the `scheduled_messages` table.

## DbSet: ConsumerMessage

Stores all messages awaiting processing by the `ConsumerMessageProcessor`.

## DbSet: ScheduledMessage

Stores all scheduled messages awaiting processing by the `ScheduledMessageProcessor`.

## DbSet: PoisonedMessage

Stores consumer messages that have reached `AsyncMonolith.MaxAttempts`, poisoned messages will then need to be manually moved back to the `consumer_messages` table or deleted.

## ConsumerMessageProcessor

A background service that periodically fetches available messages from the 'consumer_messages' table. Once a message is found, it's row-level locked to prevent other processes from fetching it. The corresponding consumer attempts to process the message. If successful, the message is removed from the `consumer_messages` table; otherwise, the processor increments the messages `attempts` by one and delays processing for a defined number of seconds (`AsyncMonolithSettings.AttemptDelay`). If the number of attempts reaches the limit defined by `AsyncMonolith.MaxAttempts`, the message is moved to the `poisoned_messages` table.

## ScheduledMessageProcessor

A background service that fetches available messages from the `scheduled_messages` table. Once found, each consumer set up to handle the payload is resolved, and a message is written to the `consumer_messages` table for each of them.

## ConsumerRegistry

Used to resolve all the consumers able to process a given payload, and resolve instances of the consumers when processing a message. The registry is populated on startup by calling `builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly());` which uses reflection to find all consumer & payload types.

## Notes 📋

- The background services wait for `AsyncMonolithSettings.ProcessorMaxDelay` seconds before fetching another batch of messages. If a full batch is fetched, the delay is reduced to `AsyncMonolithSettings.ProcessorMinDelay` seconds between cycles.
- Configuring concurrent consumer / scheduled message processors will throw a startup exception when using AsyncMonolith.Ef (due to no built in support for row level locking)
