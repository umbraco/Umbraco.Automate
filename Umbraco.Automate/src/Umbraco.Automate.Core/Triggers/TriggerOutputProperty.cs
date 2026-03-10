namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Describes a single property in a trigger's output schema.
/// Used for expression autocompletion and documentation in the canvas editor.
/// </summary>
public sealed class TriggerOutputProperty
{
    /// <summary>
    /// Gets or sets the property name as it appears in expressions (e.g. "contentName").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the CLR type of the property value.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// Gets or sets an optional description of the property.
    /// </summary>
    public string? Description { get; init; }
}
