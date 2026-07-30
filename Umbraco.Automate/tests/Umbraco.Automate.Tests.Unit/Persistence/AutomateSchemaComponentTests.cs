using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Tests for <see cref="AutomateSchemaComponent"/>, which is what guarantees Automate's schema exists
/// before any <c>UmbracoApplicationStartingNotification</c> handler can query it — notably Umbraco
/// Deploy's boot-time restore. See https://github.com/umbraco/Umbraco.Automate/issues/198.
/// </summary>
public class AutomateSchemaComponentTests
{
    [Fact]
    public async Task InitializeAsync_MigratesTheSchema_WhenTheRuntimeIsRunning()
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();

        await CreateComponent(RuntimeLevel.Run, schemaInitializer)
            .InitializeAsync(isRestarting: false, CancellationToken.None);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Below <see cref="RuntimeLevel.Run"/> there is no database to migrate against. The readiness
    /// signal is deliberately left unresolved rather than marked failed, because the CMS restarts the
    /// runtime once an install completes and initializes components again at <c>Run</c>.
    /// </summary>
    [Theory]
    [InlineData(RuntimeLevel.Unknown)]
    [InlineData(RuntimeLevel.Boot)]
    [InlineData(RuntimeLevel.Install)]
    [InlineData(RuntimeLevel.Upgrade)]
    [InlineData(RuntimeLevel.BootFailed)]
    public async Task InitializeAsync_SkipsMigration_WhenTheRuntimeIsNotRunning(RuntimeLevel runtimeLevel)
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();

        await CreateComponent(runtimeLevel, schemaInitializer)
            .InitializeAsync(isRestarting: false, CancellationToken.None);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Components are initialized again when the runtime restarts (e.g. straight after an interactive
    /// install), so the component must not assume it only ever runs once. The initializer is
    /// responsible for making the repeat call a no-op.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StillMigrates_WhenRestarting()
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();
        AutomateSchemaComponent component = CreateComponent(RuntimeLevel.Run, schemaInitializer);

        await component.InitializeAsync(isRestarting: true, CancellationToken.None);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_PassesTheCancellationTokenThrough()
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();
        using var cancellationTokenSource = new CancellationTokenSource();

        await CreateComponent(RuntimeLevel.Run, schemaInitializer)
            .InitializeAsync(isRestarting: false, cancellationTokenSource.Token);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(cancellationTokenSource.Token), Times.Once);
    }

    [Fact]
    public async Task TerminateAsync_DoesNothing()
        => await CreateComponent(RuntimeLevel.Run, new Mock<IAutomateSchemaInitializer>())
            .TerminateAsync(isRestarting: false, CancellationToken.None);

    private static AutomateSchemaComponent CreateComponent(
        RuntimeLevel runtimeLevel,
        Mock<IAutomateSchemaInitializer> schemaInitializer)
        => new(
            schemaInitializer.Object,
            Mock.Of<IRuntimeState>(x => x.Level == runtimeLevel),
            NullLogger<AutomateSchemaComponent>.Instance);
}
