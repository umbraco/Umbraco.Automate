namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Stores OAuth tokens obtained via an OpenIddict Client WebIntegration flow.
/// Stored in a dedicated table, referenced by connection settings via credential ID.
/// </summary>
public sealed class OAuthCredentials
{
    /// <summary>
    /// Gets or sets the credential ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the OpenIddict provider alias (e.g. "Slack", "GitHub").
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Gets or sets the encrypted access token.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the encrypted refresh token, if available.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the access token expiry time.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the granted scopes (space-separated).
    /// </summary>
    public string? Scopes { get; set; }

    /// <summary>
    /// Gets or sets a display label for the authenticated account (e.g. "My Workspace").
    /// </summary>
    public string? AccountLabel { get; set; }

    /// <summary>
    /// Gets or sets the date the credential was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date the credential was last modified (e.g. token refresh).
    /// </summary>
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
}
