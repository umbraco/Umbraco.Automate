using Umbraco.Automate.Core.Settings;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Settings shared by form record triggers (submitted, approved).
/// </summary>
public sealed class FormRecordTriggerSettings
{
    /// <summary>
    /// Gets or sets the form ID to filter on. Leave blank to match all forms.
    /// </summary>
    [Field(
        Label = "Form",
        Description = "Only fire for this form. Leave blank to match all forms.")]
    public string? FormId { get; set; }
}
