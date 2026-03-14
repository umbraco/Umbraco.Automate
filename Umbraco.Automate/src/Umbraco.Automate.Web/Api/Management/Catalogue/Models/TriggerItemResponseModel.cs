using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Response model for a registered trigger type.
/// </summary>
public sealed class TriggerItemResponseModel : StepTypeItemResponseModel
{
    /// <summary>The output properties available for binding expressions.</summary>
    public IReadOnlyList<TriggerOutputProperty> OutputProperties { get; set; } = [];
}
