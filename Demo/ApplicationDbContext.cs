using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Demo.Spam;
using Microsoft.EntityFrameworkCore;

namespace Demo;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

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