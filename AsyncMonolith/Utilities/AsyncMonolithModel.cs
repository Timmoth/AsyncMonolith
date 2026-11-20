using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AsyncMonolith.Utilities;

/// <summary>
///     Where AsyncMonolith's tables live, set in <c>modelBuilder.ConfigureAsyncMonolith(...)</c>.
/// </summary>
public sealed class AsyncMonolithModelOptions
{
    /// <summary>The database schema for the tables, or null for the default schema.</summary>
    public string? Schema { get; set; }

    /// <summary>
    ///     Prepended to every table name, for example <c>"jobs_"</c> gives <c>jobs_consumer_messages</c>. Default none.
    /// </summary>
    public string TablePrefix { get; set; } = "";
}

/// <summary>Finds the quoted, schema-qualified table names the model maps the entities to.</summary>
internal static class AsyncMonolithTables
{
    public static string Name<TEntity>(DbSet<TEntity> set) where TEntity : class
    {
        var context = set.GetService<ICurrentDbContext>().Context;
        var entityType = context.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException(
            $"{typeof(TEntity).Name} is not in the model. Call modelBuilder.ConfigureAsyncMonolith() in OnModelCreating.");
        return context.GetService<ISqlGenerationHelper>()
            .DelimitIdentifier(entityType.GetTableName()!, entityType.GetSchema());
    }
}

internal static class AsyncMonolithModel
{
    public static void Configure(ModelBuilder modelBuilder, AsyncMonolithModelOptions options)
    {
        modelBuilder.Entity<ConsumerMessage>(entity =>
        {
            entity.ToTable(options.TablePrefix + "consumer_messages", options.Schema);
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.DeduplicationKey, m.ConsumerType }).IsUnique();
            // Processors look for due messages on their rail.
            entity.HasIndex(m => new { m.RailId, m.AvailableAfter });
        });

        modelBuilder.Entity<PoisonedMessage>(entity =>
        {
            entity.ToTable(options.TablePrefix + "poisoned_messages", options.Schema);
            entity.HasKey(m => m.Id);
            // Listed newest first.
            entity.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<ScheduledMessage>(entity =>
        {
            entity.ToTable(options.TablePrefix + "scheduled_messages", options.Schema);
            entity.HasKey(m => m.Id);
            // The scheduler looks for due schedules; EnsureSchedule and DeleteByTag look schedules up by tag.
            entity.HasIndex(m => m.AvailableAfter);
            entity.HasIndex(m => m.Tag);
        });
    }
}
