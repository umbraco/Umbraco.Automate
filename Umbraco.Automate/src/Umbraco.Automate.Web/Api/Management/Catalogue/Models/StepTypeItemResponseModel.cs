using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

    /// <summary>The settings schema, or null if no settings.</summary>
    public EditableModelSchema? SettingsSchema { get; set; }

    /// <summary>The JSON Schema describing output data, or null if no output.</summary>
    public JsonDocument? OutputSchema { get; set; }

    /// <summary>The step type kind: "action", "controlFlow", or "trigger".</summary>
    [Required]
    public string Type { get; set; } = string.Empty;
}
