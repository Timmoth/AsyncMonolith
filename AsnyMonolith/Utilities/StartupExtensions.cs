using System.Reflection;
using AsnyMonolith.Consumers;
using AsnyMonolith.Producers;
using AsnyMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsnyMonolith.Utilities;

public static class StartupExtensions
{
    public static void AddAsyncMonolith<T>(this IServiceCollection services, Assembly assembly) where T : DbContext
    {
        services.Register(assembly);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<ProducerService<T>>();
        services.AddScoped<ScheduledMessageService<T>>();
        services.AddHostedService<ConsumerMessageProcessor<T>>();
        services.AddHostedService<ScheduledMessageProcessor<T>>();
    }

    public static void Register(this IServiceCollection services, Assembly assembly)
    {
        var consumerServiceDictionary = new Dictionary<string, Type>();
        var payloadConsumerDictionary = new Dictionary<string, List<string>>();

        var type = typeof(BaseConsumer<>);

        foreach (var consumerType in assembly.GetTypes()
                     .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } &&
                                 t.BaseType.GetGenericTypeDefinition() == type))
        {
            if (consumerType.BaseType == null || string.IsNullOrEmpty(consumerType.Name)) continue;

            // Register each consumer service
            services.AddScoped(consumerType);

            // Get the generic argument (T) of the consumer type
            var payloadType = consumerType.BaseType.GetGenericArguments()[0];

            if (consumerServiceDictionary.ContainsKey(consumerType.Name))
                throw new Exception($"Consumer: '{consumerType.Name}' already defined.");

            consumerServiceDictionary[consumerType.Name] = consumerType;

            if (!payloadConsumerDictionary.TryGetValue(payloadType.Name, out var payloadConsumers))
                payloadConsumerDictionary[payloadType.Name] = payloadConsumers = new List<string>();

            payloadConsumers.Add(consumerType.Name);
        }

        foreach (var consumerPayload in assembly.GetTypes()
                     .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IConsumerPayload)))
                     .Select(t => t.Name))
        {
            if (!payloadConsumerDictionary.ContainsKey(consumerPayload))
            {
                throw new Exception($"No consumers exist for payload: '{consumerPayload}'");
            }
        }

        services.AddSingleton(new ConsumerRegistry(consumerServiceDictionary, payloadConsumerDictionary));
    }
}