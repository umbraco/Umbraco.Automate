using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="RelationDeletedTrigger"/>.
/// </summary>
public sealed class RelationDeletedTriggerSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the trigger also fires for relations that
    /// Umbraco deletes automatically when content, media or members are saved (reference
    /// tracking). Off by default, as these fire on most content edits.
    /// </summary>
    [Field(
        Label = "Include automatic relations",
        Description = "Also fire for relations Umbraco removes automatically when content, media or members are saved (reference tracking).",
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle",
        SortOrder = 1)]
    public bool IncludeAutomaticRelations { get; set; }
}
