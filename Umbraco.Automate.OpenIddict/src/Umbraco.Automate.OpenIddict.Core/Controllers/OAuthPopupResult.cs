using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Builds the "postMessage back to the opener, then close" HTML page shared by the OAuth challenge
/// and callback controllers' popup flow. Both the human-readable body and the postMessage payload
/// are encoded, so untrusted values (e.g. a provider name taken from the request route) cannot
/// inject markup or script — see #107.
/// </summary>
internal static class OAuthPopupResult
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds a success page that reports the newly-stored credential id to the opener.
    /// </summary>
    public static ContentResult Success(string credentialId) =>
        Build(new PopupMessage(Success: true, CredentialId: credentialId));

    /// <summary>
    /// Builds a failure page that reports the given error to the opener. The error may contain
    /// untrusted input, so it is encoded before being embedded in the page.
    /// </summary>
    public static ContentResult Failure(string error) =>
        Build(new PopupMessage(Success: false, Error: error), bodyError: error);

    private static ContentResult Build(PopupMessage message, string? bodyError = null)
    {
        // JsonSerializer escapes quotes, angle brackets, etc., so the payload cannot break out of
        // the JSON string or close the surrounding <script> tag.
        var payload = JsonSerializer.Serialize(message, PayloadOptions);

        // HtmlEncoder neutralises any markup in the user-influenced error text before it lands in
        // the <p> body.
        var body = bodyError is null
            ? "Authentication successful. This window will close."
            : $"Authentication failed: {HtmlEncoder.Default.Encode(bodyError)}";

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head><title>OAuth Complete</title></head>
            <body>
                <p>{{body}}</p>
                <script>
                    if (window.opener) {
                        window.opener.postMessage({{payload}}, window.location.origin);
                    }
                    window.close();
                </script>
            </body>
            </html>
            """;

        return new ContentResult { Content = html, ContentType = "text/html" };
    }

    private sealed record PopupMessage(bool Success, string? CredentialId = null, string? Error = null)
    {
        public string Type => "oauth-complete";
    }
}
