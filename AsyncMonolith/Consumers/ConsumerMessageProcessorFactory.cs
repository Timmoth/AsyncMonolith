using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Represents a factory for creating and managing consumer message processors.
/// </summary>
/// <typeparam name="T">The type of the DbContext used by the consumer message processors.</typeparam>
public class ConsumerMessageProcessorFactory<T> : IHostedService where T : DbContext
{
    private readonly List<IHostedService> _hostedServices;
    private readonly int _instances;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsumerMessageProcessorFactory{T}" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to create instances of consumer message processors.</param>
    /// <param name="instances">The number of instances of consumer message processors to create.</param>
    public ConsumerMessageProcessorFactory(IServiceProvider serviceProvider, int instances)
    {
        _serviceProvider = serviceProvider;
        _instances = instances;
        _hostedServices = new List<IHostedService>();
    }

    /// <summary>
    ///     Starts the consumer message processors asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the operation.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _instances; i++)
        {
            var hostedService = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<T>>(_serviceProvider);
            _hostedServices.Add(hostedService);
            await hostedService.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Stops the consumer message processors asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the operation.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var hostedService in _hostedServices)
        {
            await hostedService.StopAsync(cancellationToken);
        }
    }
}