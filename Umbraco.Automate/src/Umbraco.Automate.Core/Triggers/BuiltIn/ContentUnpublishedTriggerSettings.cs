using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="ContentUnpublishedTrigger"/>.
/// </summary>
public sealed class ContentUnpublishedTriggerSettings
{
    /// <summary>
    /// Gets or sets the content type alias to filter on. If null, all content types match.
    /// </summary>
    [Field(Label = "Content Type", Description = "Only fire for this content type. Leave blank to match all.")]
    public string? ContentTypeAlias { get; set; }
}
