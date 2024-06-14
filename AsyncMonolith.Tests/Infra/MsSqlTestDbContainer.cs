using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AsyncMonolith.Tests.Infra;

public class MsSqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MsSqlBuilder().Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(ServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) => { options.UseSqlServer(ConnectionString); }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.MsSql;
    }
}