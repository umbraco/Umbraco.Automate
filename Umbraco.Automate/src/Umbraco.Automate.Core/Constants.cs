namespace Umbraco.Automate.Core;

/// <summary>
/// Constants for the Umbraco Automate package.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The package name used for registration and identification.
    /// </summary>
    public const string PackageName = "Umbraco.Automate";

    /// <summary>
    /// The prefix applied to all database migration names.
    /// </summary>
    public const string DatabaseMigrationPrefix = "UmbracoAutomate_";

    /// <summary>
    /// Property editor UI aliases contributed by Umbraco.Automate, for use on
    /// <see cref="Settings.EditableModelFieldAttribute.EditorUiAlias"/>. Provider packages
    /// can point their own settings fields at these to get the same editors.
    /// </summary>
    public static class EditorUiAliases
    {
        /// <summary>
        /// Editor for a field holding a single document key: a content tree picker with a
        /// toggle into a <c>${ }</c> binding. Must match the manifest alias in
        /// core/components/entity-key-picker/manifests.ts.
        /// </summary>
        public const string ContentKeyPicker = "Umb.Automate.ContentKeyPicker";

        /// <summary>
        /// Editor for a field holding a single media key. The media counterpart of
        /// <see cref="ContentKeyPicker"/>. Must match the manifest alias in
        /// core/components/entity-key-picker/manifests.ts.
        /// </summary>
        public const string MediaKeyPicker = "Umb.Automate.MediaKeyPicker";
    }

    /// <summary>
    /// Section constants for Umbraco.Automate.
    /// </summary>
    internal static class Sections
    {
        /// <summary>
        /// The section manifest alias. Must match the frontend manifest alias in section/constants.ts.
        /// </summary>
        public const string Automate = "Ua.Section.Automate";
    }
}
