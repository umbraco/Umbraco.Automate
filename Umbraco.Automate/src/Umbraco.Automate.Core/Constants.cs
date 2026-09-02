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
    /// Names of the HTTP clients registered by Umbraco.Automate. Shared by every outbound call
    /// the package makes — actions, notification channels and the Run Script action's
    /// <c>fetch</c> — so they all pick up the same SSRF-protected handler.
    /// </summary>
    public static class HttpClients
    {
        /// <summary>
        /// The default client. Follows redirects.
        /// </summary>
        public const string Default = "UmbracoAutomate";

        /// <summary>
        /// A non-redirecting variant, used when a caller must observe the redirect response
        /// itself (e.g. <c>fetch</c> with <c>redirect: "manual"</c>).
        /// </summary>
        public const string NoRedirect = "UmbracoAutomateNoRedirect";
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
