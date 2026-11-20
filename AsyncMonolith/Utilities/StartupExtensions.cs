using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Utilities;

/// <summary>
/// Extension methods for configuring and registering AsyncMonolith in the application startup.
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Configures the AsyncMonolith model.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static ModelBuilder ConfigureAsyncMonolith(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsumerMessage>()
            .HasIndex(e => new { e.InsertId, e.ConsumerType })
            .IsUnique();

        return modelBuilder;
    }

    internal static void InternalConfigureAsyncMonolithSettings(
        this IServiceCollection services,
        AsyncMonolithSettings settings)
    {
        if (settings.AttemptDelay < 0)
        {
            throw new ArgumentException("AsyncMonolithSettings.AttemptDelay must be positive.");
        }

        if (settings.MaxAttemptDelay < 0)
        {
            throw new ArgumentException("AsyncMonolithSettings.MaxAttemptDelay must be positive.");
        }

        if (settings.MaxAttempts < 1)
        {
            throw new ArgumentException("AsyncMonolithSettings.MaxAttempts must be at least 1.");
        }

        if (settings.ProcessorMaxDelay < 1)
        {
            throw new ArgumentException("AsyncMonolithSettings.ProcessorMaxDelay must be at least 1.");
        }

        if (settings.ProcessorMinDelay < 0)
        {
            throw new ArgumentException("AsyncMonolithSettings.ProcessorMinDelay must be positive.");
        }

        if (settings.ProcessorMinDelay > settings.ProcessorMaxDelay)
        {
            throw new ArgumentException(
                "AsyncMonolithSettings.ProcessorMaxDelay must be greater than AsyncMonolithSettings.ProcessorMinDelay.");
        }

        if (settings.ProcessorBatchSize < 1)
        {
            throw new ArgumentException("AsyncMonolithSettings.ProcessorBatchSize must be at least 1.");
        }

        if (settings.DefaultConsumerTimeout < 1)
        {
            throw new ArgumentException("AsyncMonolithSettings.DefaultConsumerTimeout must be at least 1.");
        }

        if (settings.DefaultConsumerTimeout > 3600)
        {
            throw new ArgumentException("AsyncMonolithSettings.DefaultConsumerTimeout must be less then 3600.");
        }

        services.Configure<AsyncMonolithSettings>(options =>
        {
            options.AttemptDelay = settings.AttemptDelay;
            options.MaxAttemptDelay = settings.MaxAttemptDelay;
            options.MaxAttempts = settings.MaxAttempts;
            options.ProcessorMaxDelay = settings.ProcessorMaxDelay;
            options.ProcessorMinDelay = settings.ProcessorMinDelay;
            options.ProcessorBatchSize = settings.ProcessorBatchSize;
            options.DefaultConsumerTimeout = settings.DefaultConsumerTimeout;
        });
    }

    internal static void InternalAddAsyncMonolith<T>(
        this IServiceCollection services,
        AsyncMonolithSettings settings) where T : DbContext
    {
        if (services.Any(d => d.ServiceType == typeof(AsyncMonolithRegistration)))
        {
            throw new InvalidOperationException(
                "AsyncMonolith is already registered. Call Add*AsyncMonolith once, in the host; modules register " +
                "their consumers with AddAsyncMonolithConsumers.");
        }

        services.AddSingleton<AsyncMonolithRegistration>();
        services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(settings);

        // TryAdd, so a host can register its own implementation before or instead of these.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAsyncMonolithIdGenerator, AsyncMonolithIdGenerator>();
        services.TryAddScoped<IProducerService, ProducerService<T>>();
        services.TryAddScoped<IScheduleService, ScheduleService<T>>();
        services.TryAddScoped<IPoisonedMessageService, PoisonedMessageService<T>>();
        services.TryAddScoped<IQueueStatisticsService, QueueStatisticsService<T>>();

        // One processor per rail. Rails are read when the host starts, after every module has
        // registered its consumers.
        services.AddHostedService(serviceProvider => new ConsumerMessageProcessorFactory<T>(serviceProvider,
            serviceProvider.GetRequiredService<ConsumerRegistry>().RailIds.ToArray()));

        services.AddHostedService<ScheduledMessageProcessor<T>>();
    }

    internal static void InternalRegisterAsyncMonolithConsumers(
        this IServiceCollection services,
        AsyncMonolithSettings settings)
    {
        services.AddAsyncMonolithConsumers(settings.AssembliesToRegister.ToArray());
    }

    /// <summary>
    ///     Registers the consumers and payloads in the given assemblies. Call it from each module that has
    ///     consumers; the host calls <c>Add*AsyncMonolith</c> once. Order doesn't matter, and registering the
    ///     same assembly twice is harmless.
    /// </summary>
    /// <remarks>
    ///     The consumer registry is built, and checked for name clashes and payloads without consumers, the first
    ///     time it is resolved, which happens when the host starts.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAsyncMonolithConsumers(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var registered = services
            .Where(d => d.ServiceType == typeof(ConsumerAssemblies))
            .Select(d => d.ImplementationInstance)
            .OfType<ConsumerAssemblies>()
            .FirstOrDefault();
        if (registered is null)
        {
            registered = new ConsumerAssemblies();
            services.AddSingleton(registered);
            services.AddOptions();
            services.TryAddSingleton(serviceProvider => BuildConsumerRegistry(registered,
                serviceProvider.GetRequiredService<IOptions<AsyncMonolithSettings>>().Value));
        }

        foreach (var assembly in assemblies)
        {
            if (!registered.Add(assembly))
            {
                continue;
            }

            foreach (var consumerType in FindConsumerTypes(assembly.GetTypes()))
            {
                services.TryAddScoped(consumerType);
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers the consumers and payloads in the assembly containing <typeparamref name="TMarker" />.
    /// </summary>
    /// <typeparam name="TMarker">Any type in the assembly to scan.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAsyncMonolithConsumersFromAssemblyContaining<TMarker>(
        this IServiceCollection services) => services.AddAsyncMonolithConsumers(typeof(TMarker).Assembly);

    private static IEnumerable<Type> FindConsumerTypes(IEnumerable<Type> types) =>
        types.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                         ConsumerInvoker.PayloadTypeOf(t) is not null);

    internal static ConsumerRegistry BuildConsumerRegistry(IEnumerable<Assembly> assemblies,
        AsyncMonolithSettings settings)
    {
        var consumerServiceDictionary = new Dictionary<string, Type>();
        var payloadConsumerDictionary = new Dictionary<string, List<string>>();
        var consumerTimeoutDictionary = new Dictionary<string, int>();
        var consumerAttemptsDictionary = new Dictionary<string, int>();
        var consumerExecutionModeDictionary = new Dictionary<string, ConsumerInstanceExecutionMode>();
        var payloadTypeDictionary = new Dictionary<string, Type>();

        var consumerRailIdDictionary = new Dictionary<string, int>();
        
        var typesToScan = assemblies.SelectMany(x => x.GetTypes()).ToArray();

        foreach (var consumerType in FindConsumerTypes(typesToScan))
        {
            var consumerName = AsyncMonolithNames.ForConsumer(consumerType);

            var timeoutDuration = settings.DefaultConsumerTimeout;
            var attribute = Attribute.GetCustomAttribute(consumerType, typeof(ConsumerTimeoutAttribute));
            if (attribute is ConsumerTimeoutAttribute timeoutAttribute)
            {
                timeoutDuration = timeoutAttribute.Duration;
            }

            if (timeoutDuration <= 0)
            {
                timeoutDuration = settings.DefaultConsumerTimeout;
            }

            consumerTimeoutDictionary[consumerName] = timeoutDuration;

            var maxAttempts = settings.MaxAttempts;
            attribute = Attribute.GetCustomAttribute(consumerType, typeof(ConsumerAttemptsAttribute));
            if (attribute is ConsumerAttemptsAttribute attemptsAttribute)
            {
                maxAttempts = attemptsAttribute.Attempts;
            }

            if (maxAttempts <= 0)
            {
                maxAttempts = settings.MaxAttempts;
            }

            consumerAttemptsDictionary[consumerName] = maxAttempts;

            var consumerExecutionMode = ConsumerInstanceExecutionMode.Parallel;
            attribute = Attribute.GetCustomAttribute(consumerType, typeof(ConsumerExecutionModeAttribute));
            if (attribute is ConsumerExecutionModeAttribute executionModeAttribute)
            {
                consumerExecutionMode = executionModeAttribute.ExecutionMode;
            }
            
            consumerExecutionModeDictionary[consumerName] = consumerExecutionMode;

            var consumerRailId = 0;
            attribute = Attribute.GetCustomAttribute(consumerType, typeof(ConsumerRailIdAttribute));
            if (attribute is ConsumerRailIdAttribute consumerRailIdAttribute)
            {
                consumerRailId = consumerRailIdAttribute.RailId;
            }
            
            consumerRailIdDictionary[consumerName] = consumerRailId;

            // Get the generic argument (T) of the consumer type
            var payloadType = ConsumerInvoker.PayloadTypeOf(consumerType)!;

            if (!consumerServiceDictionary.TryAdd(consumerName, consumerType))
            {
                throw new InvalidOperationException(
                    $"Consumer: '{consumerName}' already defined by '{consumerServiceDictionary[consumerName].FullName}' and '{consumerType.FullName}'. Give one a unique name with [ConsumerName].");
            }

            var payloadName = RegisterPayloadName(payloadTypeDictionary, payloadType);
            if (!payloadConsumerDictionary.TryGetValue(payloadName, out var payloadConsumers))
            {
                payloadConsumerDictionary[payloadName] = payloadConsumers = new List<string>();
            }

            payloadConsumers.Add(consumerName);
        }

        foreach (var consumerPayload in typesToScan
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IConsumerPayload)))
            .Select(t => RegisterPayloadName(payloadTypeDictionary, t)))
        {
            if (!payloadConsumerDictionary.ContainsKey(consumerPayload))
            {
                throw new InvalidOperationException($"No consumers exist for payload: '{consumerPayload}'");
            }
        }

        return new ConsumerRegistry(consumerServiceDictionary, payloadConsumerDictionary,
            consumerTimeoutDictionary, consumerAttemptsDictionary, consumerRailIdDictionary,
            consumerExecutionModeDictionary, settings);
    }

    private sealed class ConsumerAssemblies : HashSet<Assembly>;

    /// <summary>Marks that Add*AsyncMonolith has been called.</summary>
    private sealed class AsyncMonolithRegistration;

    private static string RegisterPayloadName(Dictionary<string, Type> payloadTypes, Type payloadType)
    {
        var payloadName = AsyncMonolithNames.ForPayload(payloadType);
        if (payloadTypes.TryGetValue(payloadName, out var existing) && existing != payloadType)
        {
            throw new InvalidOperationException(
                $"Payload name: '{payloadName}' is used by both '{existing.FullName}' and '{payloadType.FullName}'. Give one a unique name with [ConsumerPayloadName].");
        }

        payloadTypes[payloadName] = payloadType;
        return payloadName;
    }
}
