using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="SetVariableAction"/>.
/// </summary>
public sealed class SetVariableOutput
{
    /// <summary>
    /// Gets the variable name that was set.
    /// </summary>
    [Description("The variable name that was set.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the variable value that was set.
    /// </summary>
    [Description("The variable value that was set.")]
    public string? Value { get; init; }
}
