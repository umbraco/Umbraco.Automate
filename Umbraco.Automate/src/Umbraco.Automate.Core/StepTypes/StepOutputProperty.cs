namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Describes a single property in a step type's output schema.
/// Used for binding expression autocompletion and documentation in the canvas editor.
/// </summary>
public sealed class StepOutputProperty
{
    /// <summary>
    /// Gets or sets the property name as it appears in binding expressions (e.g. "contentName", "statusCode").
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
