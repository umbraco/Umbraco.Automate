using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="DelayAction"/>.
/// </summary>
public sealed class DelaySettings
{
    /// <summary>
    /// Gets or sets the delay duration as a TimeSpan string (e.g. "00:05:00" for 5 minutes).
    /// </summary>
    [Field(Label = "Duration", Description = "How long to wait before proceeding (e.g. 00:05:00 for 5 minutes, 00:00:30 for 30 seconds).")]
    public string Duration { get; set; } = "00:00:30";
}
