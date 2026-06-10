namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// A trigger that is fired manually via the API (e.g. a "Run now" button).
/// Has no settings or output — the automation simply begins.
/// </summary>
[Trigger("umbracoAutomate.manual", "Manual Trigger",
    Description = "Fires when the automation is triggered manually.",
    Group = "Core",
    Icon = "icon-hand-pointer")]
public sealed class ManualTrigger : TriggerBase<object, object>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManualTrigger"/> class.
    /// </summary>
    public ManualTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }
}
