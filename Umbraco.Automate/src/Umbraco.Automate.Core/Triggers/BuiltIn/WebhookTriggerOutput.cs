namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="WebhookTrigger"/> containing the incoming HTTP request data.
/// </summary>
public sealed class WebhookTriggerOutput
{
    /// <summary>
    /// Gets the HTTP method (e.g. "POST").
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Gets the raw request body as a string.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets the request headers as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = [];

    /// <summary>
    /// Gets the query string parameters as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Query { get; init; } = [];
}
