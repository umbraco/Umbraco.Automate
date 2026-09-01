using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="HttpRequestAction"/>.
/// </summary>
public sealed class HttpRequestSettings
{
    /// <summary>
    /// Gets or sets the request URL.
    /// </summary>
    [Field(Label = "URL", Description = "The URL to send the request to.", SupportsBindings = true)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method (GET, POST, PUT, DELETE, PATCH).
    /// </summary>
    [Field(Label = "Method", Description = "The HTTP method to use.", SortOrder = 1)]
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the request body (for POST, PUT, PATCH).
    /// </summary>
    [Field(
        Label = "Body",
        Description = "The request body content.",
        SortOrder = 2,
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "height", "value": 150 },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the content type header. Defaults to application/json.
    /// </summary>
    [Field(Label = "Content Type", Description = "The Content-Type header value.", SortOrder = 3)]
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets custom headers as a JSON object string (key-value pairs).
    /// </summary>
    [Field(
        Label = "Headers",
        Description = "Custom headers as JSON object (e.g. {\"Authorization\": \"Bearer token\"}).",
        SortOrder = 4,
        IsSensitive = true,
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 120 },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string? Headers { get; set; }
}
