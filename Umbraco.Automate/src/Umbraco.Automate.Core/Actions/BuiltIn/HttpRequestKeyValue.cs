namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A single key/value row used by the <see cref="HttpRequestAction"/> for both request
/// headers and <c>application/x-www-form-urlencoded</c> form fields.
/// </summary>
/// <remarks>
/// One type serves both lists on purpose: the shape, the editor UI and the binding rules are
/// identical, and a second POCO would only duplicate them. Only <see cref="Value"/> carries
/// anything secret — header and field names are not credentials — which is what lets the
/// serializer encrypt values while leaving keys readable in the key/value editor.
/// <para>
/// Named for the shape rather than for headers because <c>HttpRequestHeader</c> is taken by
/// <see cref="System.Net.HttpRequestHeader"/>, and any file that uses both would have to
/// disambiguate every reference.
/// </para>
/// </remarks>
public sealed class HttpRequestKeyValue
{
    /// <summary>
    /// Gets or sets the header or form-field name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the header or form-field value. Supports <c>${ }</c> bindings and
    /// <c>$Config:Key</c> references, both resolved before the request is sent.
    /// </summary>
    public string? Value { get; set; }
}
