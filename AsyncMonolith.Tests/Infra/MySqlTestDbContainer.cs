using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;

namespace AsyncMonolith.Tests.Infra;

public class MySqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MySqlBuilder().Build();
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

    public override DbType GetDbType() => DbType.MySql;
}