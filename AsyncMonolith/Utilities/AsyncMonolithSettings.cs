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
    ///     Gets or sets the delay in seconds before the first retry of a failed message. Each further retry
    ///     waits twice as long as the one before, up to <see cref="MaxAttemptDelay" />.
    ///     Default: 10 seconds, Min: 0 second, Max N/A
    /// </summary>
    public int AttemptDelay { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the longest delay in seconds between retries. If it is less than
    ///     <see cref="AttemptDelay" />, retries wait <see cref="AttemptDelay" /> each time.
    ///     Default: 300 seconds, Min: 0 second, Max N/A
    /// </summary>
    public int MaxAttemptDelay { get; set; } = 300;

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
    ///     Gets or sets the default number of seconds a consumer waits before timing out.
    ///     Default: 10 seconds, Min: 1 second, Max 3600 seconds
    /// </summary>
    public int DefaultConsumerTimeout { get; set; } = 10;

    /// <summary>
    ///     Gets the delay in seconds before retrying a message that has failed <paramref name="attempts" /> times.
    /// </summary>
    /// <param name="attempts">The number of failed attempts so far (1 after the first failure).</param>
    /// <returns><see cref="AttemptDelay" /> doubled for each attempt after the first, capped at
    ///     <see cref="MaxAttemptDelay" /> (or <see cref="AttemptDelay" /> if that is larger).</returns>
    public long GetRetryDelay(int attempts)
    {
        var cap = Math.Max(AttemptDelay, MaxAttemptDelay);
        var doublings = Math.Clamp(attempts - 1, 0, 30);
        return Math.Min((long)AttemptDelay << doublings, cap);
    }

    /// <summary>
    ///     Gets the default AsyncMonolithSettings.
    /// </summary>
    public static AsyncMonolithSettings Default => new()
    {
        MaxAttempts = 5,
        AttemptDelay = 10,
        MaxAttemptDelay = 300,
        ProcessorMaxDelay = 1000,
        ProcessorMinDelay = 10,
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