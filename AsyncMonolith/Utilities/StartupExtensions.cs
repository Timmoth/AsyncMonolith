using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Utilities;

public static class StartupExtensions
{
    public static void ConfigureAsyncMonolith(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsumerMessage>()
            .HasIndex(e => new { e.InsertId, e.ConsumerType })
            .IsUnique();
    }

    public static void InternalAddAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        settings ??= AsyncMonolithSettings.Default;
        if (settings.AttemptDelay < 0)
            throw new ArgumentException("AsyncMonolithSettings.AttemptDelay must be positive.");

        if (settings.MaxAttempts < 1)
            throw new ArgumentException("AsyncMonolithSettings.MaxAttempts must be at least 1.");

        if (settings.ProcessorMaxDelay < 1)
            throw new ArgumentException("AsyncMonolithSettings.ProcessorMaxDelay must be at least 1.");
        if (settings.ProcessorMinDelay < 0)
            throw new ArgumentException("AsyncMonolithSettings.ProcessorMinDelay must be positive.");
        if (settings.ProcessorMinDelay > settings.ProcessorMaxDelay)
            throw new ArgumentException(
                "AsyncMonolithSettings.ProcessorMaxDelay must be greater then AsyncMonolithSettings.ProcessorMinDelay.");

        if (settings.ConsumerMessageProcessorCount < 1)
            throw new ArgumentException("AsyncMonolithSettings.ConsumerMessageProcessorCount must be at least 1.");

        if (settings.ScheduledMessageProcessorCount < 1)
            throw new ArgumentException("AsyncMonolithSettings.ScheduledMessageProcessorCount must be at least 1.");

        if (settings.ProcessorBatchSize < 1)
            throw new ArgumentException("AsyncMonolithSettings.ProcessorBatchSize must be at least 1.");


        if (settings.DefaultConsumerTimeout < 1)
            throw new ArgumentException("AsyncMonolithSettings.DefaultConsumerTimeout must be at least 1.");

        services.Configure<AsyncMonolithSettings>(options =>
        {
            options.AttemptDelay = settings.AttemptDelay;
            options.MaxAttempts = settings.MaxAttempts;
            options.ProcessorMaxDelay = settings.ProcessorMaxDelay;
            options.ProcessorMinDelay = settings.ProcessorMinDelay;
            options.ProcessorBatchSize = settings.ProcessorBatchSize;
            options.ConsumerMessageProcessorCount = settings.ConsumerMessageProcessorCount;
            options.ScheduledMessageProcessorCount = settings.ScheduledMessageProcessorCount;
            options.DefaultConsumerTimeout = settings.DefaultConsumerTimeout;
        });

        services.Register(assembly, settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<ScheduleService<T>>();

        if (settings.ConsumerMessageProcessorCount > 1)
            services.AddHostedService(serviceProvider =>
                new ConsumerMessageProcessorFactory<T>(serviceProvider, settings.ConsumerMessageProcessorCount));
        else
            services.AddHostedService<ConsumerMessageProcessor<T>>();

        if (settings.ScheduledMessageProcessorCount > 1)
            services.AddHostedService(serviceProvider =>
                new ScheduledMessageProcessorFactory<T>(serviceProvider, settings.ScheduledMessageProcessorCount));
        else
            services.AddHostedService<ScheduledMessageProcessor<T>>();
    }

    public static void Register(this IServiceCollection services, Assembly assembly, AsyncMonolithSettings settings)
    {
        var consumerServiceDictionary = new Dictionary<string, Type>();
        var payloadConsumerDictionary = new Dictionary<string, List<string>>();
        var consumerTimeoutDictionary = new Dictionary<string, int>();

        var type = typeof(BaseConsumer<>);

        foreach (var consumerType in assembly.GetTypes()
                     .Where(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } &&
                                 t.BaseType.GetGenericTypeDefinition() == type))
        {
            if (consumerType.BaseType == null || string.IsNullOrEmpty(consumerType.Name)) continue;

            // Register each consumer service
            services.AddScoped(consumerType);

            var timeoutDuration = settings.DefaultConsumerTimeout;
            var attribute = Attribute.GetCustomAttribute(consumerType, typeof(ConsumerTimeoutAttribute));
            if (attribute is ConsumerTimeoutAttribute timeoutAttribute) timeoutDuration = timeoutAttribute.Duration;

            if (timeoutDuration <= 0) timeoutDuration = settings.DefaultConsumerTimeout;

            consumerTimeoutDictionary[consumerType.Name] = timeoutDuration;

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
            if (!payloadConsumerDictionary.ContainsKey(consumerPayload))
                throw new Exception($"No consumers exist for payload: '{consumerPayload}'");

        services.AddSingleton(new ConsumerRegistry(consumerServiceDictionary, payloadConsumerDictionary,
            consumerTimeoutDictionary, settings));
    }
}