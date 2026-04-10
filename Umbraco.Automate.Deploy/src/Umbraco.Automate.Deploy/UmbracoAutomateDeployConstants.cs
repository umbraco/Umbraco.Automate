namespace Umbraco.Automate.Deploy;

/// <summary>
/// Constants used throughout Umbraco.Automate Deploy.
/// </summary>
public static class UmbracoAutomateDeployConstants
{
    /// <summary>
    /// UDI entity type identifiers for Umbraco.Automate entities.
    /// </summary>
    public static class UdiEntityType
    {
        /// <summary>
        /// UDI entity type for automations.
        /// </summary>
        public const string Automation = "umbraco-automate-automation";

        /// <summary>
        /// UDI entity type for workspaces.
        /// </summary>
        public const string Workspace = "umbraco-automate-workspace";

        /// <summary>
        /// UDI entity type for connections.
        /// </summary>
        public const string Connection = "umbraco-automate-connection";
    }
}
