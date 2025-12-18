using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Example.Api.Spam;
using Microsoft.EntityFrameworkCore;

namespace Example.Api;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<SubmittedValue> SubmittedValues { get; set; } = default!;
    public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
    public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
    public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureAsyncMonolith();
        base.OnModelCreating(modelBuilder);
    }
}