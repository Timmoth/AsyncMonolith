using System.Text.RegularExpressions;
using AsyncMonolith.Tests.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The hand-written SQL schemas in /Schemas must match the EF model, for hosts that don't use migrations.
/// </summary>
public partial class SchemaFileTests
{
    public static TheoryData<string> SchemaFiles => new()
    {
        "asyncmonolith_postgresql.sql",
        "asyncmonolith_mssql.sql",
        "asyncmonolith_mysql.sql",
        "asyncmonolith_mariadb.sql"
    };

    private static string SchemaPath(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Schemas")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("the repository's Schemas folder should be above the test output");
        return Path.Combine(directory.FullName, "Schemas", file);
    }

    [GeneratedRegex(@"CREATE TABLE\s+(\w+)\s*\((.*?)\n\);", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CreateTable();

    // A column definition: a name followed by a type. Constraint lines are skipped.
    [GeneratedRegex(@"^\s*(?!CONSTRAINT\b|PRIMARY\b|UNIQUE\b|KEY\b)""?(\w+)""?\s+\w", RegexOptions.IgnoreCase)]
    private static partial Regex Column();

    /// <summary>Table name to (column name to nullable). Columns are nullable unless declared NOT NULL.</summary>
    private static Dictionary<string, Dictionary<string, bool>> ParseTables(string sql) =>
        CreateTable().Matches(sql).ToDictionary(
            table => table.Groups[1].Value,
            table => table.Groups[2].Value.Split('\n')
                .Select(line => (Line: line, Match: Column().Match(line)))
                .Where(c => c.Match.Success)
                .ToDictionary(
                    c => c.Match.Groups[1].Value,
                    c => !c.Line.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)));

    [Theory]
    [MemberData(nameof(SchemaFiles))]
    public async Task Schema_file_has_every_model_column_with_matching_nullability(string file)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
        var tables = ParseTables(await File.ReadAllTextAsync(SchemaPath(file), TestContext.Current.CancellationToken));

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            tables.ShouldContainKey(table, $"{file} should create {table}");

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName();
                tables[table].ShouldContainKey(column, $"{file} should have {table}.{column}");
                tables[table][column].ShouldBe(property.IsNullable,
                    $"{file}: {table}.{column} nullability should match the model");
            }
        }
    }
}
