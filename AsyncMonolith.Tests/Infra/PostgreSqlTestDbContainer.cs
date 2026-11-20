using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AsyncMonolith.Tests.Infra;

public class PostgreSqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) => { options.UseNpgsql(ConnectionString, o => { }); }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.PostgreSql;
    }
}