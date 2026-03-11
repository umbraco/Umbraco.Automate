namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Marker interface for triggers that respond to incoming HTTP webhooks.
/// Each automation with this trigger type gets a unique webhook URL.
/// </summary>
public interface IWebhookTrigger;
