namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Thrown when a settings field marked <c>BindingMustResolve</c> holds a <c>${ }</c> binding that
/// resolved to an empty or whitespace value. Derives from <see cref="InvalidOperationException"/>
/// so the step error classifier treats it as a terminal configuration error: retrying cannot help.
/// The message names the field and the binding expression but never the resolved data. For
/// sensitive fields the raw value is replaced by the masked placeholder, so literal secret text
/// around a binding never reaches the stored step error or the logs.
/// </summary>
internal sealed class SettingsBindingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsBindingException"/> class.
    /// </summary>
    /// <param name="fieldName">The field's label, or its property name when it has none.</param>
    /// <param name="expression">The raw, unevaluated field value containing the binding, or the masked placeholder for sensitive fields.</param>
    public SettingsBindingException(string fieldName, string expression)
        : base($"Setting '{fieldName}' is bound to '{expression}', which resolved to no value.")
    {
        FieldName = fieldName;
    }

    /// <summary>
    /// Gets the name of the field whose binding resolved to no value.
    /// </summary>
    public string FieldName { get; }
}
