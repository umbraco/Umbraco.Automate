namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that fires when an external system sends an HTTP request to the automation's webhook URL.
/// </summary>
[Trigger(WellKnownAlias, "Webhook",
    Description = "Fires when an HTTP request is received at this automation's webhook URL.",
    Group = "Core",
    Icon = "icon-webhook")]
public sealed class WebhookTrigger : WebhookTriggerBase<WebhookTriggerSettings, WebhookTriggerOutput>
{
    /// <summary>
    /// Well-known alias for the built-in webhook trigger.
    /// </summary>
    public const string WellKnownAlias = "umbracoAutomate.webhook";

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTrigger"/> class.
    /// </summary>
    public WebhookTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }
}
