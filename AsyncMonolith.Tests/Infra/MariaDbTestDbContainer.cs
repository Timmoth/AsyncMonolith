using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MariaDb;
using Testcontainers.MsSql;

namespace AsyncMonolith.Tests.Infra;

public class MariaDbTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MariaDbBuilder().Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(ServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) =>
            {
                options.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
            }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.MariaDb;
    }
}