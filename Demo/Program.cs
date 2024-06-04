using System.Reflection;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

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

        builder.Services.AddLogging();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly(),
            new AsyncMonolithSettings
            {
                AttemptDelay = 10,
                MaxAttempts = 5,
                ProcessorMinDelay = 50,
                ProcessorMaxDelay = 1000,
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
                scope.ServiceProvider.GetRequiredService<ScheduledMessageService<ApplicationDbContext>>();

            scheduledMessageService.Schedule(new ValueSubmitted
            {
                Value = 1
            }, "*/5 * * * * *", "UTC");
            dbContext.SaveChanges();
        }

        app.Run();
    }
}