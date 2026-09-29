using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="WebhookTrigger"/> containing the incoming HTTP request data.
/// </summary>
public sealed class WebhookTriggerOutput
{
    /// <summary>
    /// Gets the HTTP method (e.g. "POST").
    /// </summary>
    [Description("The HTTP method (e.g. \"POST\").")]
    public string? Method { get; init; }

    /// <summary>
    /// Gets the raw request body as a string.
    /// </summary>
    [Description("The raw request body as a string.")]
    public string? Body { get; init; }

    /// <summary>
    /// Gets the request headers as key-value pairs.
    /// </summary>
    [Description("The request headers as key-value pairs.")]
    public Dictionary<string, string> Headers { get; init; } = [];

    /// <summary>
    /// Gets the query string parameters as key-value pairs.
    /// </summary>
    [Description("The query string parameters as key-value pairs.")]
    public Dictionary<string, string> Query { get; init; } = [];
}
