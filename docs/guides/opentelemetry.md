Ensure you add `AsyncMonolithInstrumentation.ActivitySourceName` as a source to your OpenTelemetry configuration if you want to receive consumer / scheduled processor traces.

```csharp
        builder.Services.AddOpenTelemetry()
            .WithTracing(x =>
            {
                if (builder.Environment.IsDevelopment()) x.SetSampler<AlwaysOnSampler>();

                x.AddSource(AsyncMonolithInstrumentation.ActivitySourceName);
                x.AddConsoleExporter();
            })
            .ConfigureResource(c => c.AddService("async_monolith.demo").Build());
```

| Tag                           | Description           |
|-------------------------------|-----------------------|
| consumer_message.id           | Consumer message Id   |
| consumer_message.attempt      | Attempt number        |
| consumer_message.payload.type | Message payload type  |
| consumer_message.type         | Message consumer type |
| exception.type                | Exception type        |
| exception.message             | Exception message     |

## Metrics

AsyncMonolith publishes metrics on the `async_monolith` meter (`AsyncMonolithInstrumentation.MeterName`):

| Metric | Unit | Tags | Meaning |
|---|---|---|---|
| `async_monolith.messages.produced` | messages | `payload_type`, `consumer_type` | Messages produced, one per consumer |
| `async_monolith.messages.consumed` | messages | `consumer_type` | Messages consumed successfully |
| `async_monolith.messages.failed` | messages | `consumer_type` | Failed attempts (including the last one before poisoning) |
| `async_monolith.messages.poisoned` | messages | `consumer_type` | Messages moved to `poisoned_messages` |
| `async_monolith.consume.duration` | s | `consumer_type`, `outcome` | How long consumers took (`consumed`, `retrying`, `poisoned`) |
| `async_monolith.messages.lag` | s | `consumer_type` | Time between a message becoming due and being claimed |
| `async_monolith.schedules.fired` | occurrences | `payload_type` | Schedule occurrences that produced a message |

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(AsyncMonolithInstrumentation.MeterName));
```

A growing `messages.lag`, or `IQueueStatisticsService`'s `OldestAvailableAgeSeconds`, means processors are falling behind.
