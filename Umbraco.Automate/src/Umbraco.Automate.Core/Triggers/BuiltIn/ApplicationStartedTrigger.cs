using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires once when the Umbraco application has started.
/// </summary>
[Trigger("umbracoAutomate.applicationStarted", "Application Started",
    Description = "Fires once when the Umbraco application has started.",
    Group = "Core",
    Icon = "icon-power")]
public sealed class ApplicationStartedTrigger
    : NotificationTriggerBase<ApplicationStartedTriggerSettings, ApplicationStartedTriggerOutput, UmbracoApplicationStartedNotification>
{

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationStartedTrigger"/> class.
    /// </summary>
    /// <param name="infrastructure"></param>
    public ApplicationStartedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {        
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
                StartedAt = DateTimeOffset.UtcNow
            }
        };
    }
}
