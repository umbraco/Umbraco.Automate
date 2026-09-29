namespace Umbraco.Automate.Web.Api.Management.Run.Models;

/// <summary>
/// Response model for the recorded trigger data of a run. The value is pretty-printed JSON with
/// sensitive values masked, cut to a maximum length.
/// </summary>
public sealed class RunTriggerDataResponseModel
{
    /// <summary>
    /// The data the trigger passed to the run, as pretty-printed JSON, or <c>null</c> when the run
    /// was started without any.
    /// </summary>
    public string? TriggerData { get; set; }

    /// <summary>Whether <see cref="TriggerData"/> was truncated.</summary>
    public bool TriggerDataTruncated { get; set; }
}
