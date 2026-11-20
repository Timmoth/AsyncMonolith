using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MariaDb;
using Testcontainers.MsSql;

namespace AsyncMonolith.Tests.Infra;

public class MariaDbTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MariaDbBuilder("mariadb:11.8").Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(ServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) =>
            {
                options.UseMySQL(ConnectionString);
            }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.MariaDb;
    }
}