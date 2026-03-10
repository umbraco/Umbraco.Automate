namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Activation interface for triggers that respond to incoming HTTP webhooks.
/// Each automation with this trigger type gets a unique webhook URL.
/// </summary>
public interface IWebhookTrigger
{
    /// <summary>
    /// Gets the HTTP methods this webhook trigger accepts (e.g. "POST", "PUT").
    /// </summary>
    IEnumerable<string> AllowedMethods { get; }
}
