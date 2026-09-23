namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// How the <see cref="HttpRequestAction"/> builds the request body.
/// </summary>
public enum HttpRequestBodyMode
{
    /// <summary>
    /// Send the <c>Body</c> text verbatim with the configured <c>Content-Type</c>.
    /// The default, so automations saved before form support keep their behaviour.
    /// </summary>
    Raw = 0,

    /// <summary>
    /// Build an <c>application/x-www-form-urlencoded</c> body from the configured form
    /// fields. The Content-Type is set automatically and the configured one is ignored.
    /// </summary>
    Form = 1,
}
