namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// A subset of the web <c>RequestInit</c> options object accepted as the second argument to
/// <c>fetch</c>. Only the members meaningful to server-side execution are honoured; the rest are
/// accepted for API compatibility.
/// </summary>
public sealed class RequestInit
{
    private string _cache = "default";
    private string _credentials = "same-origin";
    private string _method = "GET";
    private string _mode = "cors";
    private string _priority = "auto";
    private string _redirect = "follow";

    /// <summary>Gets or sets the request body.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets whether topics are sent (browser-only; accepted for compatibility).</summary>
    public bool BrowsingTopics { get; set; }

    /// <summary>Gets or sets the cache mode (accepted for compatibility).</summary>
    public string Cache
    {
        get => _cache;
        set
        {
            if (value is "default" or "no-store" or "reload" or "no-cache" or "force-cache" or "only-if-cached")
            {
                _cache = value;
            }
        }
    }

    /// <summary>Gets or sets the credentials mode (accepted for compatibility).</summary>
    public string Credentials
    {
        get => _credentials;
        set
        {
            if (value is "omit" or "same-origin" or "include")
            {
                _credentials = value;
            }
        }
    }

    /// <summary>Gets or sets the request headers (a <see cref="Headers"/>, an object, or an array of pairs).</summary>
    public object? Headers { get; set; }

    /// <summary>Gets or sets the subresource integrity value (accepted for compatibility).</summary>
    public string Integrity { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the request may outlive the page (browser-only; accepted for compatibility).</summary>
    public bool Keepalive { get; set; }

    /// <summary>Gets or sets the HTTP method.</summary>
    public string Method
    {
        get => _method;
        set
        {
            // The web fetch API normalises method case, so `{ method: 'post' }` must not fall
            // through to the default GET — which would send the body on the wrong method.
            var method = value?.ToUpperInvariant();
            if (method is "GET" or "HEAD" or "POST" or "PUT" or "DELETE" or "CONNECT" or "OPTIONS" or "TRACE" or "PATCH")
            {
                _method = method;
            }
        }
    }

    /// <summary>Gets or sets the request mode (accepted for compatibility).</summary>
    public string Mode
    {
        get => _mode;
        set
        {
            if (value is "same-origin" or "cors" or "no-cors" or "navigate")
            {
                _mode = value;
            }
        }
    }

    /// <summary>Gets or sets the request priority (accepted for compatibility).</summary>
    public string Priority
    {
        get => _priority;
        set
        {
            if (value is "high" or "low" or "auto")
            {
                _priority = value;
            }
        }
    }

    /// <summary>Gets or sets how redirects are handled ("follow", "error", or "manual").</summary>
    public string Redirect
    {
        get => _redirect;
        set
        {
            if (value is "follow" or "error" or "manual")
            {
                _redirect = value;
            }
        }
    }

    /// <summary>Gets the referrer (accepted for compatibility).</summary>
    public string Referrer { get; } = "about:client";

    /// <summary>Gets the referrer policy (accepted for compatibility).</summary>
    public string? ReferrerPolicy { get; }

    /// <summary>Gets the abort signal (accepted for compatibility).</summary>
    public object? Signal { get; }
}
