using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Represents a factory for creating and managing multiple instances of <see cref="ScheduledMessageProcessor{T}" /> as
///     hosted services.
/// </summary>
/// <typeparam name="T">The type of the <see cref="DbContext" /> used by the <see cref="ScheduledMessageProcessor{T}" />.</typeparam>
public class ScheduledMessageProcessorFactory<T> : IHostedService where T : DbContext
{
    private readonly List<IHostedService> _hostedServices;
    private readonly int _instances;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScheduledMessageProcessorFactory{T}" /> class.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider" /> used to resolve dependencies.</param>
    /// <param name="instances">The number of instances of <see cref="ScheduledMessageProcessor{T}" /> to create and manage.</param>
    public ScheduledMessageProcessorFactory(IServiceProvider serviceProvider, int instances)
    {
        _serviceProvider = serviceProvider;
        _instances = instances;
        _hostedServices = new List<IHostedService>();
    }

    /// <summary>
    ///     Starts all the instances of <see cref="ScheduledMessageProcessor{T}" /> as hosted services.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to stop the operation.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _instances; i++)
        {
            var hostedService = ActivatorUtilities.CreateInstance<ScheduledMessageProcessor<T>>(_serviceProvider);
            _hostedServices.Add(hostedService);
            await hostedService.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Stops all the instances of <see cref="ScheduledMessageProcessor{T}" /> as hosted services.
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