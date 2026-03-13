using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SetVariableAction"/>.
/// </summary>
public sealed class SetVariableActionSettings
{
    /// <summary>
    /// Gets or sets the variable name. Supports expression syntax.
    /// </summary>
    [Field(Label = "Name", Description = "The name of the variable to set. Supports ${ expression } syntax.", SupportsExpressions = true)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable value. Supports expression syntax.
    /// </summary>
    [Field(Label = "Value", Description = "The value to assign to the variable. Supports ${ expression } syntax.", SortOrder = 1, SupportsExpressions = true)]
    public string Value { get; set; } = string.Empty;
}
