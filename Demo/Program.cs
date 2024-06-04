using System.Reflection;
using AsnyMonolith.Scheduling;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Demo;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var dbId = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(dbId);
            }
        );

        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(),
            new AsyncMonolithSettings
            {
                AttemptDelay = 10,
                MaxAttempts = 5,
                ProcessorMinDelay = 10,
                ProcessorMaxDelay = 1000
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
            var scheduledMessageService =
                scope.ServiceProvider.GetRequiredService<ScheduledMessageService<ApplicationDbContext>>();

            scheduledMessageService.Schedule(new ValueSubmitted
            {
                Value = Random.Shared.NextDouble() * 100
            }, "*/5 * * * * *", "UTC");
            dbContext.SaveChanges();
        }

        app.Run();
    }
}