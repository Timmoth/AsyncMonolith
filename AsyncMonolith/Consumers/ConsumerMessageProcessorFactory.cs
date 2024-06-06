using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncMonolith.Consumers;

public class ConsumerMessageProcessorFactory<T> : IHostedService where T : DbContext
{
    private readonly List<IHostedService> _hostedServices;
    private readonly int _instances;
    private readonly IServiceProvider _serviceProvider;

    public ConsumerMessageProcessorFactory(IServiceProvider serviceProvider, int instances)
    {
        _serviceProvider = serviceProvider;
        _instances = instances;
        _hostedServices = new List<IHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _instances; i++)
        {
            var hostedService = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<T>>(_serviceProvider);
            _hostedServices.Add(hostedService);
            await hostedService.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var hostedService in _hostedServices) await hostedService.StopAsync(cancellationToken);
    }
}