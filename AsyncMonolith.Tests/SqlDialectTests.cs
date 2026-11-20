using AsyncMonolith.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The fetch SQL is chosen from the EF Core provider the DbContext uses.
/// </summary>
public class SqlDialectTests
{
    private sealed class AnyDbContext(DbContextOptions options) : DbContext(options);

    private static SqlDialect DialectOf(Action<DbContextOptionsBuilder> configure)
    {
        var builder = new DbContextOptionsBuilder();
        configure(builder);
        using var db = new AnyDbContext(builder.Options);
        return SqlDialects.For(db);
    }

    [Fact]
    public void Npgsql_uses_PostgreSQL_queries() =>
        DialectOf(o => o.UseNpgsql("Host=localhost")).ShouldBe(SqlDialect.PostgreSql);

    [Fact]
    public void SqlServer_uses_SQL_Server_queries() =>
        DialectOf(o => o.UseSqlServer("Server=localhost")).ShouldBe(SqlDialect.SqlServer);

    [Fact]
    public void Oracles_MySQL_provider_uses_MySQL_queries() =>
        DialectOf(o => o.UseMySQL("Server=localhost")).ShouldBe(SqlDialect.MySql);

    [Fact]
    public void SQLite_uses_generic_queries() =>
        DialectOf(o => o.UseSqlite("Data Source=:memory:")).ShouldBe(SqlDialect.Generic);

    [Theory]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", "MySql")]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory", "Generic")]
    [InlineData("Some.Future.Provider", "Generic")]
    [InlineData(null, "Generic")]
    public void Other_providers_are_mapped_by_name(string? provider, string expected) =>
        SqlDialects.ForProvider(provider).ToString().ShouldBe(expected);
}
