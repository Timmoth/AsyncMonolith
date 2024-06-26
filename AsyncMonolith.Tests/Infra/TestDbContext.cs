using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Tests.Infra;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<ConsumerMessage> ConsumerMessages { get; set; } = default!;
    public DbSet<PoisonedMessage> PoisonedMessages { get; set; } = default!;
    public DbSet<ScheduledMessage> ScheduledMessages { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureAsyncMonolith();
        base.OnModelCreating(modelBuilder);
    }
}