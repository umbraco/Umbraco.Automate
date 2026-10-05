using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Models;

/// <summary>
/// A named result a step type can finish with. Authors route each outcome to its own next step.
/// </summary>
public class StepOutcomeResponseModel
{
    /// <summary>The stable identifier of the outcome.</summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>The display label: a <c>#key</c> localization key or literal text, returned untranslated.</summary>
    [Required]
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether this is the default outcome, used when the step succeeds without naming an outcome.</summary>
    public bool IsDefault { get; set; }
}
