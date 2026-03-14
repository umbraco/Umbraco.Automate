namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that fires when an external system sends an HTTP request to the automation's webhook URL.
/// </summary>
[Trigger("umbracoAutomate.webhook", "Webhook",
    Description = "Fires when an HTTP request is received at this automation's webhook URL.",
    Group = "Core",
    Icon = "icon-webhook")]
public sealed class WebhookTrigger : WebhookTriggerBase<WebhookTriggerSettings, WebhookTriggerOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTrigger"/> class.
    /// </summary>
    public WebhookTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }
}
