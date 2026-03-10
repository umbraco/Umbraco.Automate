namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// A trigger that is fired manually via the API (e.g. a "Run now" button).
/// Has no settings or output — the automation simply begins.
/// </summary>
[Trigger("umbracoAutomate.manual", "Manual Trigger")]
public sealed class ManualTrigger : TriggerBase<object, object>
{
    /// <inheritdoc />
    public override string? Description => "Fires when the automation is triggered manually.";

    /// <inheritdoc />
    public override string? Group => "Core";

    /// <inheritdoc />
    public override string? Icon => "icon-hand-pointer";
}
