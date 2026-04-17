using Microsoft.AspNetCore.Http;

namespace Umbraco.Automate.Core.Triggers.Webhooks;

/// <summary>
/// Validates the authenticity of an incoming webhook request.
/// Provider packages can register custom authenticators for services like GitHub, Stripe, etc.
/// </summary>
public interface IWebhookAuthenticator
{
    /// <summary>
    /// Gets the unique alias for this authenticator (e.g. "hmac-sha256", "github", "stripe").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets a human-readable display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional short description shown in the authentication strategy picker
    /// (e.g. the header/query parameter the caller must send).
    /// </summary>
    string? Description => null;

    /// <summary>
    /// Gets a value indicating whether this authenticator needs the request body during validation
    /// (e.g. HMAC body-signing). When <c>false</c>, validation runs before the body is read which
    /// lets callers fail fast with 401 for unauthorized requests on large payloads.
    /// </summary>
    bool RequiresBody => true;

    /// <summary>
    /// Validates the webhook request.
    /// </summary>
    /// <param name="context">The webhook authentication context containing the request, body, and configured secret.</param>
    /// <returns><c>true</c> if the request is authentic; otherwise <c>false</c>.</returns>
    bool Validate(WebhookAuthenticationContext context);
}

/// <summary>
/// Context passed to <see cref="IWebhookAuthenticator.Validate"/> containing
/// the HTTP request details and configured secret.
/// </summary>
public sealed class WebhookAuthenticationContext
{
    /// <summary>
    /// Gets the incoming HTTP request.
    /// </summary>
    public required HttpRequest Request { get; init; }

    /// <summary>
    /// Gets the raw request body, or null if no body was sent.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets the secret configured on the webhook trigger settings.
    /// </summary>
    public required string Secret { get; init; }
}
