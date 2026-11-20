using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;

namespace AsyncMonolith.Tests.Infra;

public class MySqlTestDbContainer : TestDbContainerBase
{
    public override async Task InitializeContainerAsync()
    {
        var container = new MySqlBuilder("mysql:8.4").Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
        DbContainer = container;
    }

    public override void AddDb(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>((sp, options) =>
            {
                options.UseMySQL(ConnectionString);
            }
        );
    }

    public override DbType GetDbType()
    {
        return DbType.MySql;
    }
}