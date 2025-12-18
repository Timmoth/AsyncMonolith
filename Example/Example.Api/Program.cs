using System.Reflection;
using AsyncMonolith.PostgreSql;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Example.Api.Counter;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Example.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString =
            builder.Configuration.GetConnectionString("postgresdb");
        
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString,
                name: "postgres",
                tags: new[] { "ready" });

        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddPostgreSqlAsyncMonolith<ApplicationDbContext>(settings =>
        {
            settings.RegisterTypesFromAssembly(Assembly.GetExecutingAssembly());
            settings.AttemptDelay = 10;
            settings.MaxAttempts = 5;
            settings.ProcessorMinDelay = 10;
            settings.ProcessorMaxDelay = 100;
            settings.ProcessorBatchSize = 10;
        });
        
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    tracing.SetSampler<AlwaysOnSampler>();
                }

                tracing.AddSource(AsyncMonolithInstrumentation.ActivitySourceName);
                tracing.AddConsoleExporter();
            })
            .ConfigureResource(r =>
                r.AddService("async_monolith.demo").Build());
        
        builder.Services.AddControllers();
        builder.Services.AddScoped<TotalValueService>();

        // Blazor Server
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();

        app.MapStaticAssets();
        app.UseAntiforgery();

        app.MapRazorComponents<AsyncMonolith.Web.Components.App>()
            .AddInteractiveServerRenderMode();

        app.MapControllers();

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        });
        
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();

            var scheduler = scope.ServiceProvider
                .GetRequiredService<IScheduleService>();

            scheduler.Schedule(
                new ValueSubmitted { Value = 1 },
                "*/1 * * * * *",
                "UTC");

            dbContext.SaveChanges();
        }

        app.Run();
    }
}
