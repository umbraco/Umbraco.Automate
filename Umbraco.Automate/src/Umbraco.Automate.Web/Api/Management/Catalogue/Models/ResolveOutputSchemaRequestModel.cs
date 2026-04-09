namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Request model for resolving a step type's output schema based on its configured settings.
/// </summary>
public sealed class ResolveOutputSchemaRequestModel
{
    /// <summary>The step's current settings values.</summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}
