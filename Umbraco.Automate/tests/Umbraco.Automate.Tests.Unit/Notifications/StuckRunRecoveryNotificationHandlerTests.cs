using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Automate.Core;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Notifications;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Notifications;

/// <summary>
/// Every other startup gate (workflow host, outbox dispatcher, startup validation) waits on
/// <see cref="AutomateReadinessSignal"/> and skips gracefully when Automate's startup migrations
/// fail. This handler used to skip that check entirely and query the database unconditionally,
/// which crashed the whole Umbraco host with an unhandled exception when the schema didn't exist
/// yet (e.g. no connection string configured).
/// </summary>
public class StuckRunRecoveryNotificationHandlerTests
{
    private readonly Mock<IDbContextFactory<UmbracoAutomateDbContext>> _dbContextFactory = new(MockBehavior.Strict);
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private readonly AutomateReadinessSignal _readinessSignal = new();
    private readonly StuckRunRecoveryNotificationHandler _handler;

    public StuckRunRecoveryNotificationHandlerTests()
    {
        _serverRoleAccessor.Setup(r => r.CurrentServerRole).Returns(ServerRole.Single);

        _handler = new StuckRunRecoveryNotificationHandler(
            _dbContextFactory.Object,
            _serverRoleAccessor.Object,
            _readinessSignal,
            Mock.Of<ILogger<StuckRunRecoveryNotificationHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ShouldNotTouchDatabase_WhenStartupMigrationsFailed()
    {
        _readinessSignal.SignalFailed(new InvalidOperationException("Simulated migration failure"));

        await _handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        _dbContextFactory.Verify(
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
