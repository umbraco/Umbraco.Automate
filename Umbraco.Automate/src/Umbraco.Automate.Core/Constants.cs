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
