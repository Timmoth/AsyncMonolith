using AsyncMonolith.Consumers;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;
using Xunit;

namespace AsyncMonolith.TestHelpers
{
    /// <summary>
    /// Base class for consumer test classes.
    /// </summary>
    public abstract class ConsumerTestBase : IAsyncLifetime
    {
        private DateTime _startTime;
        private LogLevel _logLevel;
        protected FakeTimeProvider FakeTime { get; private set; } = default!;

        /// <summary>
        /// Sets up the services for the test.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected abstract Task Setup(IServiceCollection services);

        private async Task<ServiceProvider> Setup()
        {
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddFilter(logLevel => logLevel >= _logLevel);
                b.AddXUnit(TestOutput);
            });

            FakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z"));
            services.AddSingleton<TimeProvider>(FakeTime);
            await Setup(services);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerTestBase"/> class.
        /// </summary>
        /// <param name="testOutput">The test output helper.</param>
        /// <param name="logLevel">The log level.</param>
        protected ConsumerTestBase(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Information)
        {
            TestOutput = testOutput;
            _logLevel = logLevel;
        }

        /// <summary>
        /// Gets the test output helper.
        /// </summary>
        public ITestOutputHelper TestOutput { get; }

        /// <summary>
        /// Gets the service provider.
        /// </summary>
        public IServiceProvider Services { get; private set; } = default!;

        /// <summary>
        /// Initializes the test asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task InitializeAsync()
        {
            _startTime = DateTime.Now;
            TestOutput.WriteLine($"[Lifecycle] Initialise {_startTime.ToString(CultureInfo.InvariantCulture)}");
            Services = await Setup();
        }

        /// <summary>
        /// Disposes the test asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task DisposeAsync()
        {
            TestOutput.WriteLine($"[Lifecycle] Dispose ({(DateTime.Now - _startTime).TotalSeconds}s)");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes the consumer message.
        /// </summary>
        /// <typeparam name="T">The type of the consumer.</typeparam>
        /// <typeparam name="V">The type of the payload.</typeparam>
        /// <param name="payload">The payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected async Task Process<T, V>(V payload, CancellationToken cancellationToken = default) where T : BaseConsumer<V> where V : IConsumerPayload
        {
            using var scope = Services.CreateScope();
            await TestConsumerMessageProcessor.Process<T, V>(scope, payload, cancellationToken);
        }
    }
}
