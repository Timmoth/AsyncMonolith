using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Utilities;

/// <summary>
///     Options for AsyncMonolith. Set them in the <c>Add*AsyncMonolith</c> call, or bind them from configuration
///     with <see cref="AsyncMonolithBuilder.BindConfiguration" />. They are validated when the host starts.
/// </summary>
public sealed class AsyncMonolithOptions
{
    internal HashSet<Assembly> AssembliesToRegister { get; } = [];

    /// <summary>
    ///     How many times a message is attempted before it is poisoned. Default 5, at least 1.
    ///     Override per consumer with <c>[ConsumerAttempts]</c>.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    ///     The delay before the first retry of a failed message. Each further retry waits twice as long as the one
    ///     before, up to <see cref="MaxAttemptDelay" />. Default 10 seconds, not negative.
    /// </summary>
    public TimeSpan AttemptDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The longest delay between retries. If it is less than <see cref="AttemptDelay" />, retries wait
    ///     <see cref="AttemptDelay" /> each time. Default 5 minutes, not negative.
    /// </summary>
    public TimeSpan MaxAttemptDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     How long a consumer may run before it is cancelled and the attempt counts as failed. Default 10 seconds,
    ///     a whole number of seconds between 1 second and 1 hour. Override per consumer with <c>[ConsumerTimeout]</c>.
    /// </summary>
    public TimeSpan DefaultConsumerTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>The most messages (or schedules) a processor takes at once. Default 5, at least 1.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>
    ///     How long a processor waits before looking again when its last batch wasn't full. Default 1 second, at
    ///     least 1 millisecond.
    /// </summary>
    public TimeSpan IdlePollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     How long a processor waits before looking again when its last batch was full. Default 10 milliseconds,
    ///     not negative and not more than <see cref="IdlePollInterval" />.
    /// </summary>
    public TimeSpan BusyPollInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    ///     Gets the delay before retrying a message that has failed <paramref name="attempts" /> times.
    /// </summary>
    /// <param name="attempts">The number of failed attempts so far (1 after the first failure).</param>
    /// <returns><see cref="AttemptDelay" /> doubled for each attempt after the first, capped at
    ///     <see cref="MaxAttemptDelay" /> (or <see cref="AttemptDelay" /> if that is larger).</returns>
    public TimeSpan GetRetryDelay(int attempts)
    {
        var cap = AttemptDelay > MaxAttemptDelay ? AttemptDelay : MaxAttemptDelay;
        var ticks = AttemptDelay.Ticks;
        for (var i = 1; i < attempts && ticks < cap.Ticks; i++)
        {
            ticks *= 2;
        }

        return TimeSpan.FromTicks(Math.Min(ticks, cap.Ticks));
    }

    /// <summary>Registers the consumers and payloads in the assembly containing <typeparamref name="T" />.</summary>
    public AsyncMonolithOptions RegisterTypesFromAssemblyContaining<T>()
        => RegisterTypesFromAssemblyContaining(typeof(T));

    /// <summary>Registers the consumers and payloads in the assembly containing <paramref name="type" />.</summary>
    public AsyncMonolithOptions RegisterTypesFromAssemblyContaining(Type type)
        => RegisterTypesFromAssembly(type.Assembly);

    /// <summary>Registers the consumers and payloads in an assembly.</summary>
    public AsyncMonolithOptions RegisterTypesFromAssembly(Assembly assembly)
        => RegisterTypesFromAssemblies(assembly);

    /// <summary>Registers the consumers and payloads in the given assemblies.</summary>
    public AsyncMonolithOptions RegisterTypesFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            AssembliesToRegister.Add(assembly);
        }

        return this;
    }
}

/// <summary>Validates <see cref="AsyncMonolithOptions" /> when the host starts.</summary>
internal sealed class AsyncMonolithOptionsValidator : IValidateOptions<AsyncMonolithOptions>
{
    public ValidateOptionsResult Validate(string? name, AsyncMonolithOptions options)
    {
        var errors = new List<string>();
        if (options.MaxAttempts < 1)
        {
            errors.Add("MaxAttempts must be at least 1.");
        }

        if (options.AttemptDelay < TimeSpan.Zero)
        {
            errors.Add("AttemptDelay must not be negative.");
        }

        if (options.MaxAttemptDelay < TimeSpan.Zero)
        {
            errors.Add("MaxAttemptDelay must not be negative.");
        }

        if (options.DefaultConsumerTimeout < TimeSpan.FromSeconds(1) ||
            options.DefaultConsumerTimeout > TimeSpan.FromHours(1) ||
            options.DefaultConsumerTimeout.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            errors.Add("DefaultConsumerTimeout must be a whole number of seconds between 1 second and 1 hour.");
        }

        if (options.BatchSize < 1)
        {
            errors.Add("BatchSize must be at least 1.");
        }

        if (options.IdlePollInterval < TimeSpan.FromMilliseconds(1))
        {
            errors.Add("IdlePollInterval must be at least 1 millisecond.");
        }

        if (options.BusyPollInterval < TimeSpan.Zero)
        {
            errors.Add("BusyPollInterval must not be negative.");
        }

        if (options.BusyPollInterval > options.IdlePollInterval)
        {
            errors.Add("BusyPollInterval must not be more than IdlePollInterval.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors.Select(e => $"AsyncMonolith: {e}"));
    }
}

/// <summary>
///     Returned by <c>Add*AsyncMonolith</c> to continue configuring AsyncMonolith.
/// </summary>
public sealed class AsyncMonolithBuilder
{
    internal AsyncMonolithBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>The service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    ///     Binds <see cref="AsyncMonolithOptions" /> from a configuration section, for example
    ///     <c>"AsyncMonolith"</c>. Values from configuration are applied after those set in code.
    /// </summary>
    public AsyncMonolithBuilder BindConfiguration(string sectionPath)
    {
        Services.AddOptions<AsyncMonolithOptions>().BindConfiguration(sectionPath);
        return this;
    }

    /// <summary>Registers the consumers and payloads in more assemblies.</summary>
    public AsyncMonolithBuilder AddConsumers(params Assembly[] assemblies)
    {
        Services.AddAsyncMonolithConsumers(assemblies);
        return this;
    }

    /// <summary>Registers the consumers and payloads in the assembly containing <typeparamref name="TMarker" />.</summary>
    public AsyncMonolithBuilder AddConsumersFromAssemblyContaining<TMarker>() =>
        AddConsumers(typeof(TMarker).Assembly);
}
