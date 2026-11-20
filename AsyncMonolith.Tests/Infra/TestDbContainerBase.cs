using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests.Infra;

public abstract class TestDbContainerBase : IAsyncLifetime
{
    protected IContainer DbContainer { get; set; } = default!;
    public string ConnectionString { get; protected set; } = default!;
    public DbType DbType => GetDbType();

    public async ValueTask InitializeAsync()
    {
        await InitializeContainerAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DbContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public abstract Task InitializeContainerAsync();

    public abstract void AddDb(IServiceCollection services);
    public abstract DbType GetDbType();
}