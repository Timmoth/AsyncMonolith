using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Tests.Infra;

/// <summary>A host DbContext with no AsyncMonolith DbSets and prefixed table names.</summary>
public class PrefixedDbContext(DbContextOptions<PrefixedDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureAsyncMonolith(o => o.TablePrefix = "jobs_");
}

/// <summary>A host DbContext with AsyncMonolith's tables in their own schema, for databases that support schemas.</summary>
public class SchemaDbContext(DbContextOptions<SchemaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureAsyncMonolith(o =>
        {
            o.Schema = "async_monolith";
            o.TablePrefix = "app_";
        });
}
