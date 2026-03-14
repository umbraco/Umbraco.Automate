using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SetVariableAction"/>.
/// </summary>
public sealed class SetVariableSettings
{
    /// <summary>
    /// Gets or sets the variable name. Supports binding syntax.
    /// </summary>
    [Field(Label = "Name", Description = "The name of the variable to set. Supports ${ binding } syntax.", SupportsBindings = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable value. Supports binding syntax.
    /// </summary>
    [Field(Label = "Value", Description = "The value to assign to the variable. Supports ${ binding } syntax.", SortOrder = 1, SupportsBindings = true)]
    public string Value { get; set; } = string.Empty;
}
