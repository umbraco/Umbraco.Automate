using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="DelayAction"/>.
/// </summary>
public sealed class DelayOutput
{
    /// <summary>
    /// Gets the duration that was delayed for, as a formatted string.
    /// </summary>
    [Description("The duration that was delayed for, as a formatted string.")]
    public string DelayedFor { get; init; } = string.Empty;
}
