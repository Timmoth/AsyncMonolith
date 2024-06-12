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
