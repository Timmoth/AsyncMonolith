using Microsoft.EntityFrameworkCore;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests.Infra;

public abstract class TestDbContainerBase : IAsyncLifetime
{
    protected IContainer? DbContainer { get; set; }
    public string ConnectionString { get; protected set; } = default!;
    public DbType DbType => GetDbType();

    public async ValueTask InitializeAsync()
    {
        await InitializeContainerAsync();
        await WaitUntilConnectable();
    }

    /// <summary>
    ///     A container can report ready a moment before the database accepts connections (MySQL restarts once while
    ///     initialising), so wait until a connection succeeds before handing it to a test.
    /// </summary>
    private async Task WaitUntilConnectable()
    {
        var services = new ServiceCollection();
        AddDb(services);
        await using var provider = services.BuildServiceProvider();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            try
            {
                if (await db.Database.CanConnectAsync())
                {
                    return;
                }
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                // Not accepting connections yet
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"The {GetDbType()} test database never accepted a connection.");
            }

            await Task.Delay(500);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (DbContainer is not null)
        {
            await DbContainer.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    public abstract Task InitializeContainerAsync();

    public abstract void AddDb(IServiceCollection services);
    public abstract DbType GetDbType();
}