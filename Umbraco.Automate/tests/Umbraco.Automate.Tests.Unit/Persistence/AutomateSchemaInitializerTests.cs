using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Failure and once-only behaviour of <see cref="AutomateSchemaInitializer"/>. The success path needs
/// a real database and lives in the integration tests.
/// </summary>
public class AutomateSchemaInitializerTests
{
    /// <summary>
    /// A broken schema must disable Automate, not take the site down with it. This runs during
    /// component initialization, where a thrown exception would abort the whole Umbraco boot.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_RecordsTheFailure_WithoutThrowing()
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal);

        await initializer.EnsureMigratedAsync();

        readinessSignal.HasFailed.ShouldBeTrue();
        readinessSignal.IsReady.ShouldBeFalse();
    }

    /// <summary>
    /// Waiters must fail fast rather than hang on a signal that will never arrive.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_MakesWaitersFailFast_WhenMigrationFails()
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal);

        await initializer.EnsureMigratedAsync();

        AutomateNotReadyException exception =
            await Should.ThrowAsync<AutomateNotReadyException>(() => readinessSignal.WaitAsync());
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// The initializer is called from both a component and a startup notification handler, and
    /// components are initialized again on a runtime restart. A repeat call must not re-run the
    /// migration, and a failed migration must not be retried on every subsequent call.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_OnlyAttemptsOnce()
    {
        var logger = new Mock<ILogger<AutomateSchemaInitializer>>();
        using AutomateSchemaInitializer initializer =
            CreateInitializer(new AutomateReadinessSignal(), logger.Object);

        await initializer.EnsureMigratedAsync();
        await initializer.EnsureMigratedAsync();
        await initializer.EnsureMigratedAsync();

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureMigratedAsync_IsSafeToCallConcurrently()
    {
        var logger = new Mock<ILogger<AutomateSchemaInitializer>>();
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal, logger.Object);

        await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => initializer.EnsureMigratedAsync())));

        readinessSignal.HasFailed.ShouldBeTrue();
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Below <see cref="RuntimeLevel.Run"/> there is no database to migrate against, so nothing is
    /// attempted and the signal is left unresolved rather than faulted.
    /// </summary>
    [Theory]
    [InlineData(RuntimeLevel.Unknown)]
    [InlineData(RuntimeLevel.Boot)]
    [InlineData(RuntimeLevel.Install)]
    [InlineData(RuntimeLevel.Upgrade)]
    [InlineData(RuntimeLevel.BootFailed)]
    public async Task EnsureMigratedAsync_DoesNothing_WhenTheRuntimeIsNotRunning(RuntimeLevel runtimeLevel)
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer =
            CreateInitializer(readinessSignal, runtimeLevel: runtimeLevel);

        await initializer.EnsureMigratedAsync();

        readinessSignal.IsReady.ShouldBeFalse();
        readinessSignal.HasFailed.ShouldBeFalse();
    }

    /// <summary>
    /// Regression test. This type is a singleton and its "attempted" latch outlives a runtime restart,
    /// so a call made below <see cref="RuntimeLevel.Run"/> — the startup notification handler is
    /// published at <c>Install</c> too — must not consume the single attempt. Otherwise the restart
    /// that follows an interactive install would skip migrating, and Automate would stay permanently
    /// not-ready for the rest of the process.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_StillAttempts_WhenAnEarlierCallWasBelowRun()
    {
        var readinessSignal = new AutomateReadinessSignal();
        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.SetupSequence(x => x.Level)
            .Returns(RuntimeLevel.Install)
            .Returns(RuntimeLevel.Run);

        using AutomateSchemaInitializer initializer =
            CreateInitializer(readinessSignal, runtimeState: runtimeState.Object);

        await initializer.EnsureMigratedAsync();
        readinessSignal.HasFailed.ShouldBeFalse("the Install-level call must not consume the attempt");

        await initializer.EnsureMigratedAsync();

        // The second call ran for real. It still fails here, because this fixture has no connection
        // string — the point is that it was attempted at all rather than skipped as already done.
        readinessSignal.HasFailed.ShouldBeTrue();
    }

    /// <summary>
    /// Builds an initializer whose migration is guaranteed to fail, by leaving Automate's connection
    /// string unconfigured — <see cref="Core.Persistence.DatabaseConnectionInfo.Resolve(IOptionsMonitor{ConnectionStrings}, IConfiguration)"/>
    /// throws <see cref="InvalidOperationException"/> in that case.
    /// </summary>
    private static AutomateSchemaInitializer CreateInitializer(
        AutomateReadinessSignal readinessSignal,
        ILogger<AutomateSchemaInitializer>? logger = null,
        RuntimeLevel runtimeLevel = RuntimeLevel.Run,
        IRuntimeState? runtimeState = null)
        => new(
            new ConfigurationBuilder().Build(),
            Mock.Of<IOptionsMonitor<ConnectionStrings>>(),
            readinessSignal,
            runtimeState ?? Mock.Of<IRuntimeState>(x => x.Level == runtimeLevel),
            logger ?? Mock.Of<ILogger<AutomateSchemaInitializer>>());
}
