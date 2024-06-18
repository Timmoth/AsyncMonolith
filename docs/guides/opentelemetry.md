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
