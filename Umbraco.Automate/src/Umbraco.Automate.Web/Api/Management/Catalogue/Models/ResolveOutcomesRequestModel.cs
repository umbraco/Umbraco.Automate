namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Request model for resolving a step type's outcomes based on its configured settings.
/// </summary>
public sealed class ResolveOutcomesRequestModel
{
    /// <summary>The step's current settings values, used as sent (binding expressions are not resolved).</summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}
