using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class RunOpenIddictMigrationNotificationHandlerTests
{
    /// <summary>
    /// Reproduces https://github.com/umbraco/Umbraco.Automate/issues/487: Umbraco publishes
    /// <see cref="UmbracoApplicationStartedNotification"/> below <see cref="RuntimeLevel.Run"/> too, where a
    /// fresh site or a DB-less CI boot has no connection string, so the handler must not try to resolve one.
    /// </summary>
    [Theory]
    [InlineData(RuntimeLevel.Install)]
    [InlineData(RuntimeLevel.Upgrade)]
    [InlineData(RuntimeLevel.BootFailed)]
    public async Task HandleAsync_BelowRun_SkipsWithoutResolvingTheConnectionString(RuntimeLevel level)
    {
        var connectionStrings = new Mock<IOptionsMonitor<ConnectionStrings>>(MockBehavior.Strict);
        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.SetupGet(s => s.Level).Returns(level);

        var handler = new RunOpenIddictMigrationNotificationHandler(
            new ConfigurationBuilder().Build(),
            connectionStrings.Object,
            runtimeState.Object,
            NullLogger<RunOpenIddictMigrationNotificationHandler>.Instance);

        await Should.NotThrowAsync(
            () => handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None));

        connectionStrings.VerifyNoOtherCalls();
    }
}
