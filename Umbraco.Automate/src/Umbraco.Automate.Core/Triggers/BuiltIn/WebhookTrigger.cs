namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that fires when an external system sends an HTTP request to the automation's webhook URL.
/// </summary>
[Trigger("umbracoAutomate.webhook", "Webhook")]
public sealed class WebhookTrigger : WebhookTriggerBase<WebhookTriggerSettings, WebhookTriggerOutput>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTrigger"/> class.
    /// </summary>
    public WebhookTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override string? Description => "Fires when an HTTP request is received at this automation's webhook URL.";

    /// <inheritdoc />
    public override string? Group => "Core";

    /// <inheritdoc />
    public override string? Icon => "icon-webhook";
}
