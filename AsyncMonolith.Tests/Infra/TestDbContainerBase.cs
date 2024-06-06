using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests.Infra;

public abstract class TestDbContainerBase : IAsyncLifetime
{
    protected IContainer DbContainer { get; set; } = default!;
    protected string ConnectionString { get; set; } = default!;
    public DbType DbType => GetDbType();

    public async Task InitializeAsync()
    {
        await InitializeContainerAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContainer.DisposeAsync().AsTask();
    }

    public abstract Task InitializeContainerAsync();

    public abstract void AddDb(ServiceCollection services);
    public abstract DbType GetDbType();
}