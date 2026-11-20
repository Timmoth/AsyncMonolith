using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AsyncMonolith.Tests.Infra;

public class MsSqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        // Kept before starting, so a container that fails to start is still cleaned up.
        DbContainer = container;
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
    }

    public override void AddDb(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) => { options.UseSqlServer(ConnectionString); }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.MsSql;
    }
}