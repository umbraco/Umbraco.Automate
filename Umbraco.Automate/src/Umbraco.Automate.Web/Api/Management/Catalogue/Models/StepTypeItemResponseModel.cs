using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// Base response model for all registered step types (actions, control flow, triggers).
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

    /// <summary>The settings schema, or null if no settings.</summary>
    public EditableModelSchema? SettingsSchema { get; set; }

    /// <summary>The output schema describing properties available for binding expressions, or null if no output.</summary>
    public IReadOnlyList<StepOutputProperty>? OutputSchema { get; set; }

    /// <summary>The step type kind: "action", "controlFlow", or "trigger".</summary>
    [Required]
    public string Type { get; set; } = string.Empty;
}
