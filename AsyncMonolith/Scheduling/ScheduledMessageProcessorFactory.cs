using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncMonolith.Scheduling;

public class ScheduledMessageProcessorFactory<T> : IHostedService where T : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _instances;
    private readonly List<IHostedService> _hostedServices;

    public ScheduledMessageProcessorFactory(IServiceProvider serviceProvider, int instances)
    {
        _serviceProvider = serviceProvider;
        _instances = instances;
        _hostedServices = new List<IHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _instances; i++)
        {
            var hostedService = ActivatorUtilities.CreateInstance<ScheduledMessageProcessor<T>>(_serviceProvider);
            _hostedServices.Add(hostedService);
            await hostedService.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var hostedService in _hostedServices)
        {
            await hostedService.StopAsync(cancellationToken);
        }
    }
}