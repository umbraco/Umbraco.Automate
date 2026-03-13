namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// EF Core entity for the <c>umbracoAutomateOpenIddictCredential</c> table.
/// </summary>
internal sealed class OAuthCredentialsEntity
{
    public Guid Id { get; set; }

    public required string Provider { get; set; }

    public required string AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public string? Scopes { get; set; }

    public string? AccountLabel { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }
}
