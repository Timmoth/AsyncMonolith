using System.Reflection;

namespace AsyncMonolith.Utilities;

/// <summary>
///     Represents the settings for the AsyncMonolith application.
/// </summary>
public class AsyncMonolithSettings
{
    internal readonly HashSet<Assembly> AssembliesToRegister = [];

    /// <summary>
    ///     Gets or sets the maximum number of attempts for processing a message.
    ///     Default: 5, Min: 1, Max N/A
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the delay in seconds between attempts for processing a failed message.
    ///     Default: 10 seconds, Min: 0 second, Max N/A
    /// </summary>
    public int AttemptDelay { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the maximum delay in milliseconds between processor cycles.
    ///     Default: 1000ms, Min: 1ms, Max N/A
    /// </summary>
    public int ProcessorMaxDelay { get; set; } = 1000;

    /// <summary>
    ///     Gets or sets the minimum delay in milliseconds between processor cycles.
    ///     Default: 10ms, Min: 0ms, Max N/A
    /// </summary>
    public int ProcessorMinDelay { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the number of messages to process in a batch.#
    ///     Default: 5, Min: 1, Max N/A
    /// </summary>
    public int ProcessorBatchSize { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the number of consumer message processors to be ran for each app instance.
    ///     Default: 1, Min: 1, Max N/A
    /// </summary>
    public int ConsumerMessageProcessorCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the number of scheduled message processors to be ran for each app instance.
    ///     Default: 1, Min: 1, Max N/A
    /// </summary>
    public int ScheduledMessageProcessorCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the default number of seconds a consumer waits before timing out.
    ///     Default: 10 seconds, Min: 1 second, Max 3600 seconds
    /// </summary>
    public int DefaultConsumerTimeout { get; set; } = 10;

    /// <summary>
    ///     Gets the default AsyncMonolithSettings.
    /// </summary>
    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 10,
        ConsumerMessageProcessorCount = 1,
        ScheduledMessageProcessorCount = 1,
        ProcessorBatchSize = 5
    };
    
    /// <summary>
    /// Register consumers and payloads from assembly containing given type.
    /// </summary>
    /// <typeparam name="T">Type from assembly to scan.</typeparam>
    /// <returns>The current instance to continue configuration.</returns>
    public AsyncMonolithSettings RegisterTypesFromAssemblyContaining<T>()
        => RegisterTypesFromAssemblyContaining(typeof(T));

    /// <summary>
    /// Register consumers and payloads from assembly containing given type.
    /// </summary>
    /// <param name="type">Type from assembly to scan.</param>
    /// <returns>The current instance to continue configuration.</returns>
    public AsyncMonolithSettings RegisterTypesFromAssemblyContaining(Type type)
        => RegisterTypesFromAssembly(type.Assembly);

    /// <summary>
    /// Register consumers and payloads from assembly.
    /// </summary>
    /// <param name="assembly">Assembly to scan</param>
    /// <returns>The current instance to continue configuration.</returns>
    public AsyncMonolithSettings RegisterTypesFromAssembly(Assembly assembly)
        => RegisterTypesFromAssemblies([assembly]);

    /// <summary>
    /// Register consumers and payloads from assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>The current instance to continue configuration.</returns>
    public AsyncMonolithSettings RegisterTypesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            AssembliesToRegister.Add(assembly);
        }

        return this;
    }
}