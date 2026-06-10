namespace Umbraco.Automate.Core.Triggers.Webhooks;

/// <summary>
/// Marks a class as a webhook authentication strategy and provides discovery metadata.
/// Apply to classes deriving from <see cref="WebhookAuthenticatorBase{TSettings}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WebhookAuthenticatorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookAuthenticatorAttribute"/> class.
    /// </summary>
    /// <param name="alias">Unique alias for the authenticator (e.g. "plain-secret", "hmac-sha256", "github").</param>
    /// <param name="name">Human-readable display name shown in the strategy picker.</param>
    public WebhookAuthenticatorAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a short description of how callers must authenticate, shown below the
    /// strategy picker to help users configure outbound webhooks on the calling service.
    /// </summary>
    public string? Description { get; set; }
}
