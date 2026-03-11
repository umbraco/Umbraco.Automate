using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="WebhookTrigger"/>.
/// </summary>
public sealed class WebhookTriggerSettings
{
    /// <summary>
    /// Gets or sets the HTTP methods this webhook accepts (e.g. "POST", "PUT").
    /// Defaults to POST only.
    /// </summary>
    [Field(Label = "Allowed Methods", Description = "HTTP methods this webhook accepts.")]
    public List<string> AllowedMethods { get; set; } = ["POST"];

    /// <summary>
    /// Gets or sets the secret used to authenticate incoming webhook requests.
    /// Validated via the <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter.
    /// Auto-generated on first save if empty.
    /// </summary>
    [Field(Label = "Webhook Secret", Description = "Secret token for authenticating incoming requests. Auto-generated if left empty.", IsSensitive = true)]
    public string? Secret { get; set; }
}
