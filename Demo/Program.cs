using System.Reflection;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Demo.Counter;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Demo;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseNpgsql(
                    "Host=async_monolith_demo_postgres;Port=5432;Username=postgres;Password=mypassword;Database=application",
                    o => { });
            }
        );

        builder.Services.AddOpenTelemetry()
            .WithTracing(x =>
            {
                if (builder.Environment.IsDevelopment()) x.SetSampler<AlwaysOnSampler>();

                x.AddSource(AsyncMonolithInstrumentation.ActivitySourceName);
                x.AddConsoleExporter();
            })
            .ConfigureResource(c => c.AddService("async_monolith.demo").Build());


        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(),
            new AsyncMonolithSettings
            {
                AttemptDelay = 10,
                MaxAttempts = 5,
                ProcessorMinDelay = 20,
                ProcessorMaxDelay = 100,
                ConsumerMessageProcessorCount = 1,
                ScheduledMessageProcessorCount = 1,
                ConsumerMessageBatchSize = 10,
                DbType = DbType.PostgreSql
            });

        builder.Services.AddControllers();
        builder.Services.AddScoped<TotalValueService>();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();

            var scheduledMessageService =
                scope.ServiceProvider.GetRequiredService<ScheduleService<ApplicationDbContext>>();

            scheduledMessageService.Schedule(new ValueSubmitted
            {
                Value = 1
            }, "*/5 * * * * *", "UTC");
            dbContext.SaveChanges();
        }

        app.Run();
    }
}