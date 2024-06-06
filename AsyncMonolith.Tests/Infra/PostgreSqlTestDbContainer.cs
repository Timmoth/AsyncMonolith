using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AsyncMonolith.Tests.Infra;

public class PostgreSqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new PostgreSqlBuilder().Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(ServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) => { options.UseNpgsql(ConnectionString, o => { }); }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.PostgreSql;
    }
}