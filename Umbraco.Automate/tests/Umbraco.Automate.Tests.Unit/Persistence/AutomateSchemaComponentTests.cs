using Umbraco.Automate.Persistence;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Tests for <see cref="AutomateSchemaComponent"/>, which is what guarantees Automate's schema exists
/// before any <c>UmbracoApplicationStartingNotification</c> handler can query it — notably Umbraco
/// Deploy's boot-time restore. See https://github.com/umbraco/Umbraco.Automate/issues/198.
/// </summary>
/// <remarks>
/// The component only decides <em>when</em> to initialize, not <em>whether</em> it is possible: the
/// runtime-level check and the once-per-process latch belong to <see cref="IAutomateSchemaInitializer"/>
/// and are covered by <see cref="AutomateSchemaInitializerTests"/>.
/// </remarks>
public class AutomateSchemaComponentTests
{
    [Fact]
    public async Task InitializeAsync_MigratesTheSchema()
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();

        await CreateComponent(schemaInitializer).InitializeAsync(isRestarting: false, CancellationToken.None);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        await CreateComponent(schemaInitializer).InitializeAsync(isRestarting: true, CancellationToken.None);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_PassesTheCancellationTokenThrough()
    {
        var schemaInitializer = new Mock<IAutomateSchemaInitializer>();
        using var cancellationTokenSource = new CancellationTokenSource();

        await CreateComponent(schemaInitializer)
            .InitializeAsync(isRestarting: false, cancellationTokenSource.Token);

        schemaInitializer.Verify(x => x.EnsureMigratedAsync(cancellationTokenSource.Token), Times.Once);
    }

    [Fact]
    public async Task TerminateAsync_DoesNothing()
        => await CreateComponent(new Mock<IAutomateSchemaInitializer>())
            .TerminateAsync(isRestarting: false, CancellationToken.None);

    private static AutomateSchemaComponent CreateComponent(Mock<IAutomateSchemaInitializer> schemaInitializer)
        => new(schemaInitializer.Object);
}
