using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SetVariableAction"/>.
/// </summary>
public sealed class SetVariableSettings
{
    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    [Field(Label = "Name", Description = "The name of the variable to set.", SupportsBindings = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable value.
    /// </summary>
    [Field(Label = "Value", Description = "The value to assign to the variable.", SortOrder = 1, SupportsBindings = true)]
    public string Value { get; set; } = string.Empty;
}
