using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// The return target for the same-tab OAuth flow used when the browser blocks the authentication
/// popup. The property editor navigates the whole tab to the challenge endpoint with a
/// <c>returnUrl</c> and a per-tab <c>nonce</c>; both are validated here, round-tripped inside the
/// OpenIddict-protected state token (never as query parameters the provider could tamper with), and
/// the callback redirects back to the URL with the result and the nonce in the URL fragment.
/// </summary>
/// <remarks>
/// The nonce lets the editor reject a result it did not ask for: without it, anyone could send a
/// user a backoffice link ending in <c>#automate-oauth=1&amp;credentialToken=...</c> and bind the
/// victim's connection to the attacker's own provider account. The editor keeps the nonce in
/// <c>sessionStorage</c>, which is per tab, so a crafted link opened elsewhere never matches.
/// </remarks>
internal sealed record OAuthReturnUrl(string Url, string Nonce)
{
    /// <summary>
    /// The <see cref="AuthenticationProperties"/> item key the validated return URL is stored under
    /// between the challenge and the callback.
    /// </summary>
    public const string UrlPropertyKey = "Automate.ReturnUrl";

    /// <summary>
    /// The <see cref="AuthenticationProperties"/> item key the nonce is stored under.
    /// </summary>
    public const string NoncePropertyKey = "Automate.ReturnNonce";

    /// <summary>
    /// The fragment key the property editor looks for when the tab comes back from the provider.
    /// </summary>
    public const string FragmentKey = "automate-oauth";

    private const int MaxNonceLength = 128;

    /// <summary>
    /// Validates the raw challenge parameters. Returns <c>true</c> with a <c>null</c>
    /// <paramref name="target"/> when neither is supplied (popup flow), <c>true</c> with a target
    /// when both are valid (same-tab flow), and <c>false</c> for anything else.
    /// </summary>
    public static bool TryCreate(string? returnUrl, string? nonce, out OAuthReturnUrl? target)
    {
        target = null;

        if (returnUrl is null && nonce is null)
        {
            return true;
        }

        if (!IsSafe(returnUrl) || !IsValidNonce(nonce))
        {
            return false;
        }

        target = new OAuthReturnUrl(returnUrl!, nonce!);
        return true;
    }

    /// <summary>
    /// Reads a return target back from the callback's authentication properties, re-validating it
    /// as defence in depth. Returns <c>null</c> for the popup flow or anything invalid.
    /// </summary>
    public static OAuthReturnUrl? ReadFrom(AuthenticationProperties? properties)
    {
        if (properties is null
            || !properties.Items.TryGetValue(UrlPropertyKey, out var url)
            || !properties.Items.TryGetValue(NoncePropertyKey, out var nonce))
        {
            return null;
        }

        return TryCreate(url, nonce, out var target) ? target : null;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="url"/> is a same-origin, root-relative path that is
    /// safe to redirect to. This mirrors ASP.NET Core's <c>IUrlHelper.IsLocalUrl</c>: absolute URLs,
    /// protocol-relative URLs (<c>//host</c>), backslash variants (<c>/\host</c>) and any URL
    /// containing control characters (browsers strip tabs/newlines, so <c>/\t/host</c> would become
    /// <c>//host</c>) are all rejected. App-relative <c>~/</c> paths are not accepted either — the
    /// property editor always sends <c>location.pathname</c>.
    /// </summary>
    public static bool IsSafe(string? url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
        {
            return false;
        }

        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
        {
            return false;
        }

        foreach (var c in url)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Stores this target in the authentication properties OpenIddict protects in its state token.
    /// </summary>
    public void WriteTo(AuthenticationProperties properties)
    {
        properties.Items[UrlPropertyKey] = Url;
        properties.Items[NoncePropertyKey] = Nonce;
    }

    /// <summary>
    /// Redirects back to the backoffice with the token for the newly-stored credential in the fragment.
    /// The fragment is used rather than the query string so the token is not sent to the server
    /// (request logs) or leaked via the <c>Referer</c> header.
    /// </summary>
    public RedirectResult Success(string provider, string credentialToken) =>
        Build(provider, ("credentialToken", credentialToken));

    /// <summary>
    /// Redirects back to the backoffice with an error message in the fragment. The property editor
    /// only ever renders it as notification text.
    /// </summary>
    public RedirectResult Failure(string provider, string error) =>
        Build(provider, ("error", error));

    private RedirectResult Build(string provider, (string Key, string Value) result)
    {
        // The return URL carries no fragment of its own (the editor sends pathname + search), but
        // strip one defensively so ours is the only fragment.
        var hashIndex = Url.IndexOf('#');
        var baseUrl = hashIndex >= 0 ? Url[..hashIndex] : Url;

        var fragment =
            $"{FragmentKey}=1" +
            $"&provider={Uri.EscapeDataString(provider)}" +
            $"&nonce={Uri.EscapeDataString(Nonce)}" +
            $"&{result.Key}={Uri.EscapeDataString(result.Value)}";

        return new RedirectResult($"{baseUrl}#{fragment}");
    }

    // The editor sends crypto.randomUUID(); accept any short URL-safe token so a caller is not tied
    // to that exact format, but nothing that could smuggle extra fragment parameters.
    private static bool IsValidNonce(string? nonce)
    {
        if (string.IsNullOrEmpty(nonce) || nonce.Length > MaxNonceLength)
        {
            return false;
        }

        foreach (var c in nonce)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
