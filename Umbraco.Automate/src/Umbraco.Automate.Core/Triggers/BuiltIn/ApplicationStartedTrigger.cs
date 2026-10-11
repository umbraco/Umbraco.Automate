using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when the Umbraco application has started. Raised on every start, restart or
/// app pool recycle, and on every server in a load-balanced setup — use
/// <see cref="ApplicationStartedTriggerSettings.OnlyRunOnMainServer"/> to limit it to the
/// main server and <see cref="ApplicationStartedTriggerOutput.IsRestarting"/> to tell a
/// cold start from a restart.
/// </summary>
[Trigger("umbracoAutomate.applicationStarted", "Application Started",
    Description = "Fires when the Umbraco application starts, including every restart or app pool recycle. In a load-balanced setup it fires on each server unless \"Only run on the main server\" is enabled.",
    Group = "Core",
    Icon = "icon-power")]
public sealed class ApplicationStartedTrigger
    : NotificationTriggerBase<ApplicationStartedTriggerSettings, ApplicationStartedTriggerOutput, UmbracoApplicationStartedNotification>
{
    private readonly IServerRoleAccessor _serverRoleAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationStartedTrigger"/> class.
    /// </summary>
    public ApplicationStartedTrigger(TriggerInfrastructure infrastructure, IServerRoleAccessor serverRoleAccessor)
        : base(infrastructure)
    {
        _serverRoleAccessor = serverRoleAccessor;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UmbracoApplicationStartedNotification notification)
    {
        yield return new TriggerEvent<ApplicationStartedTriggerOutput>
        {
            TriggerAlias = Alias,
            InitiatorType = TriggerInitiatorType.System,
            Output = new ApplicationStartedTriggerOutput
            {
                StartedAt = DateTimeOffset.UtcNow,
                IsRestarting = notification.IsRestarting,
                ServerRole = _serverRoleAccessor.CurrentServerRole.ToString(),
            },
        };
    }

    /// <inheritdoc />
    protected override bool CanHandle(ApplicationStartedTriggerOutput output, ApplicationStartedTriggerSettings? settings)
        => settings?.OnlyRunOnMainServer != true
           || !string.Equals(output.ServerRole, nameof(ServerRole.Subscriber), StringComparison.OrdinalIgnoreCase);
}
