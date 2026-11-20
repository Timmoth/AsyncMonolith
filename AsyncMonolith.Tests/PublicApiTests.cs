using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.TestHelpers;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Pins the types each package exports. Every public type is a compatibility promise, so adding or removing
///     one should be a deliberate change to this list.
/// </summary>
public class PublicApiTests
{
    private static string[] ExportedTypes(Assembly assembly) =>
        assembly.GetExportedTypes().Select(t => t.FullName!).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void Core_package_exports_only_the_intended_types()
    {
        ExportedTypes(typeof(IConsumer).Assembly).ShouldBe([
            "AsyncMonolith.Consumers.AsyncMonolithNames",
            "AsyncMonolith.Consumers.ConsumeContext",
            "AsyncMonolith.Consumers.ConsumerAttemptsAttribute",
            "AsyncMonolith.Consumers.ConsumerExecutionMode",
            "AsyncMonolith.Consumers.ConsumerExecutionModeAttribute",
            "AsyncMonolith.Consumers.ConsumerMessage",
            "AsyncMonolith.Consumers.ConsumerNameAttribute",
            "AsyncMonolith.Consumers.ConsumerPayloadNameAttribute",
            "AsyncMonolith.Consumers.ConsumerRailIdAttribute",
            "AsyncMonolith.Consumers.ConsumerTimeoutAttribute",
            "AsyncMonolith.Consumers.IConsumer",
            "AsyncMonolith.Consumers.IConsumerPayload",
            "AsyncMonolith.Consumers.IConsumer`1",
            "AsyncMonolith.Consumers.IPoisonedMessageService",
            "AsyncMonolith.Consumers.IQueueStatisticsService",
            "AsyncMonolith.Consumers.PoisonedMessage",
            "AsyncMonolith.Consumers.QueueStatistics",
            "AsyncMonolith.Producers.IProducerService",
            "AsyncMonolith.Scheduling.IScheduleService",
            "AsyncMonolith.Scheduling.ScheduledMessage",
            "AsyncMonolith.Utilities.AsyncMonolithBuilder",
            "AsyncMonolith.Utilities.AsyncMonolithInstrumentation",
            "AsyncMonolith.Utilities.AsyncMonolithOptions",
            "AsyncMonolith.Utilities.IAsyncMonolithIdGenerator",
            "AsyncMonolith.Utilities.StartupExtensions"
        ]);
    }

    [Theory]
    [InlineData("AsyncMonolith.Ef")]
    [InlineData("AsyncMonolith.PostgreSql")]
    [InlineData("AsyncMonolith.MsSql")]
    [InlineData("AsyncMonolith.MySql")]
    [InlineData("AsyncMonolith.MariaDb")]
    public void Provider_packages_export_only_their_registration_extensions(string assemblyName)
    {
        ExportedTypes(Assembly.Load(assemblyName)).ShouldBe([$"{assemblyName}.StartupExtensions"]);
    }

    [Fact]
    public void TestHelpers_package_exports_only_the_intended_types()
    {
        ExportedTypes(typeof(FakeProducerService).Assembly).ShouldBe([
            "AsyncMonolith.TestHelpers.ConsumerMessageTestHelpers",
            "AsyncMonolith.TestHelpers.ConsumerTestBase",
            "AsyncMonolith.TestHelpers.FakeIdGenerator",
            "AsyncMonolith.TestHelpers.FakeProducerService",
            "AsyncMonolith.TestHelpers.FakeScheduleService",
            "AsyncMonolith.TestHelpers.SetupTestHelpers",
            "AsyncMonolith.TestHelpers.TestConsumerMessageProcessor"
        ]);
    }
}
