using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SwitchAction"/>.
/// </summary>
public sealed class SwitchActionSettings
{
    /// <summary>
    /// Gets or sets the expression to evaluate and match against cases.
    /// </summary>
    [Field(Label = "Expression",
        Description = "The value to match against cases.",
        SupportsExpressions = true)]
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comma-separated case values to match against.
    /// A "default" outcome is used when no case matches.
    /// </summary>
    [Field(Label = "Cases",
        Description = "Comma-separated case values to match against. A 'default' outcome is used when no case matches.",
        SortOrder = 1)]
    public string Cases { get; set; } = string.Empty;
}
