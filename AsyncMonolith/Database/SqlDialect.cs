using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Database;

/// <summary>The SQL used to fetch due rows, chosen from the EF Core provider in use.</summary>
internal enum SqlDialect
{
    /// <summary>LINQ without row locks: works with any provider, such as SQLite.</summary>
    Generic,

    /// <summary>PostgreSQL: FOR UPDATE SKIP LOCKED.</summary>
    PostgreSql,

    /// <summary>SQL Server: WITH (ROWLOCK, UPDLOCK, READPAST).</summary>
    SqlServer,

    /// <summary>MySQL and MariaDB: FOR UPDATE SKIP LOCKED.</summary>
    MySql
}

internal static class SqlDialects
{
    private static readonly ConcurrentDictionary<string, SqlDialect> ByProvider = new();

    /// <summary>Picks the dialect for an EF Core provider name, such as Npgsql.EntityFrameworkCore.PostgreSQL.</summary>
    public static SqlDialect ForProvider(string? providerName) =>
        providerName is null ? SqlDialect.Generic : ByProvider.GetOrAdd(providerName, Detect);

    public static SqlDialect For(DbContext context) => ForProvider(context.Database.ProviderName);

    private static SqlDialect Detect(string providerName) => providerName switch
    {
        "Npgsql.EntityFrameworkCore.PostgreSQL" => SqlDialect.PostgreSql,
        "Microsoft.EntityFrameworkCore.SqlServer" => SqlDialect.SqlServer,
        "MySql.EntityFrameworkCore" or "Pomelo.EntityFrameworkCore.MySql" => SqlDialect.MySql,
        _ => SqlDialect.Generic
    };
}
