namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Response model for a registered trigger type.
/// </summary>
public sealed class TriggerItemResponseModel : StepTypeItemResponseModel
{
    /// <summary>
    /// Whether an automation using this trigger can be started on demand ("Run now") rather than
    /// only by the event the trigger waits on.
    /// </summary>
    public bool SupportsManualRun { get; set; }
}
