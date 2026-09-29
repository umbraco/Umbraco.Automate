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
    /// Gets or sets how the request body is built. Defaults to <see cref="HttpRequestBodyMode.Raw"/>
    /// so automations saved before form support keep sending <see cref="Body"/> verbatim.
    /// </summary>
    [Field(
        Label = "Body type",
        Description = "Raw sends the body text as-is (JSON, XML, ...). Form fields builds an application/x-www-form-urlencoded body and sets the Content-Type for you.",
        SortOrder = 2,
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """[{ "alias": "items", "value": ["Raw", "Form"] }]""")]
    public HttpRequestBodyMode BodyMode { get; set; } = HttpRequestBodyMode.Raw;

    /// <summary>
    /// Gets or sets the raw request body (for POST, PUT, PATCH), used when
    /// <see cref="BodyMode"/> is <see cref="HttpRequestBodyMode.Raw"/>.
    /// </summary>
    [Field(
        Label = "Body",
        Description = "The request body content. Used when the body type is Raw.",
        SortOrder = 3,
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "plaintext" },
                { "alias": "height", "value": 150 },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the form fields sent as an <c>application/x-www-form-urlencoded</c> body,
    /// used when <see cref="BodyMode"/> is <see cref="HttpRequestBodyMode.Form"/>.
    /// </summary>
    [Field(
        Label = "Form fields",
        Description = "The fields sent as an application/x-www-form-urlencoded body. Used when the body type is Form.",
        SortOrder = 4,
        SupportsBindings = true,
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.KeyValueEditor")]
    public List<HttpRequestKeyValue> FormFields { get; set; } = [];

    /// <summary>
    /// Gets or sets the content type header for a raw body. Defaults to application/json.
    /// Ignored when <see cref="BodyMode"/> is <see cref="HttpRequestBodyMode.Form"/>.
    /// </summary>
    [Field(Label = "Content Type", Description = "The Content-Type header value for a raw body.", SortOrder = 5)]
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets the custom request headers.
    /// </summary>
    /// <remarks>
    /// The whole property is sensitive, so <c>EditableModelSerializer</c> encrypts each row's
    /// value at rest (keys stay readable) and the transfer/export path strips the list entirely.
    /// Values saved before this became a list — a JSON object string such as
    /// <c>{"Authorization":"Bearer x"}</c> — are migrated on read by
    /// <see cref="HttpRequestKeyValueListJsonConverter"/>.
    /// </remarks>
    [Field(
        Label = "Headers",
        Description = "Custom request headers, one row per header.",
        SortOrder = 6,
        IsSensitive = true,
        SupportsBindings = true,
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.KeyValueEditor")]
    public List<HttpRequestKeyValue> Headers { get; set; } = [];
}
