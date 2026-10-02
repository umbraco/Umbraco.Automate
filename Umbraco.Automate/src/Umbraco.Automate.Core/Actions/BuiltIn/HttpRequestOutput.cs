using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="HttpRequestAction"/>.
/// </summary>
public sealed class HttpRequestOutput
{
    /// <summary>
    /// Gets the HTTP status code returned by the server.
    /// </summary>
    [Description("The HTTP status code returned by the server.")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets the response body as a string.
    /// </summary>
    [Description("The response body as a string.")]
    public string? ResponseBody { get; init; }

    /// <summary>
    /// Gets whether the response indicates success (2xx status code).
    /// </summary>
    [Description("Whether the response indicates success (2xx status code).")]
    public bool IsSuccess { get; init; }
}
