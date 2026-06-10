using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Base response model for all registered step types (actions, control flows, triggers).
/// </summary>
public class StepTypeItemResponseModel
{
    /// <summary>The step type alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>The category group.</summary>
    public string? Group { get; set; }

    /// <summary>The icon alias.</summary>
    public string? Icon { get; set; }

    /// <summary>The connection type alias required by this step type, or null if no connection is needed.</summary>
    public string? ConnectionTypeAlias { get; set; }

    /// <summary>The settings schema, or null if no settings.</summary>
    public EditableModelSchema? SettingsSchema { get; set; }

    /// <summary>The JSON Schema describing output data, or null if no output.</summary>
    public Dictionary<string, object?>? OutputSchema { get; set; }

    /// <summary>Whether this step type supports dynamic output schema resolution based on settings.</summary>
    public bool HasDynamicOutputSchema { get; set; }

    /// <summary>The step type kind: "action", "controlFlow", or "trigger".</summary>
    [Required]
    public string Type { get; set; } = string.Empty;
}
