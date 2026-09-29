namespace Umbraco.Automate.Web.Api.Management.Run.Models;

/// <summary>
/// Response model for the recorded input and output of a single step run. Values are
/// pretty-printed JSON with sensitive values masked, cut to a maximum length.
/// </summary>
public sealed class StepRunDataResponseModel
{
    /// <summary>
    /// What the step received — its settings after bindings were resolved — as pretty-printed
    /// JSON, or <c>null</c> when none was recorded (including steps run before input was recorded).
    /// </summary>
    public string? Input { get; set; }

    /// <summary>Whether <see cref="Input"/> was truncated.</summary>
    public bool InputTruncated { get; set; }

    /// <summary>
    /// What the step produced, as pretty-printed JSON, or <c>null</c> when it produced nothing.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>Whether <see cref="Output"/> was truncated.</summary>
    public bool OutputTruncated { get; set; }
}
