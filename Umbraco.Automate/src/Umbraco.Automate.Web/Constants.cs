namespace Umbraco.Automate.Web;

/// <summary>
/// Defines constants for the Umbraco Automate Management API.
/// </summary>
public class Constants
{
    /// <summary>
    /// The root namespace for the application.
    /// </summary>
    public const string AppNamespaceRoot = "Umbraco.Automate";

    /// <summary>
    /// Constants for the Automate Management API.
    /// </summary>
    public static class ManagementApi
    {
        /// <summary>
        /// The API root path.
        /// </summary>
        public const string BackofficePath = "/automate/management/api";

        /// <summary>
        /// The API title.
        /// </summary>
        public const string ApiTitle = "Umbraco Automate Management API";

        /// <summary>
        /// The API name.
        /// </summary>
        public const string ApiName = "automate-management";

        /// <summary>
        /// The namespace prefix for Automate Management API.
        /// </summary>
        public const string ApiNamespacePrefix = $"{AppNamespaceRoot}.Web.Api.Management";

        /// <summary>
        /// Feature area constants.
        /// </summary>
        public static class Feature
        {
            /// <summary>
            /// Automation feature constants.
            /// </summary>
            public static class Automation
            {
                /// <summary>The route segment.</summary>
                public const string RouteSegment = "automations";

                /// <summary>The Swagger group name.</summary>
                public const string GroupName = "Automations";
            }

            /// <summary>
            /// Run feature constants.
            /// </summary>
            public static class Run
            {
                /// <summary>The route segment.</summary>
                public const string RouteSegment = "runs";

                /// <summary>The Swagger group name.</summary>
                public const string GroupName = "Runs";
            }

            /// <summary>
            /// Workspace feature constants.
            /// </summary>
            public static class Workspace
            {
                /// <summary>The route segment.</summary>
                public const string RouteSegment = "workspaces";

                /// <summary>The Swagger group name.</summary>
                public const string GroupName = "Workspaces";
            }

            /// <summary>
            /// Connection feature constants.
            /// </summary>
            public static class Connection
            {
                /// <summary>The route segment.</summary>
                public const string RouteSegment = "connections";

                /// <summary>The Swagger group name.</summary>
                public const string GroupName = "Connections";
            }

            /// <summary>
            /// Catalogue feature constants (triggers + actions registry).
            /// </summary>
            public static class Catalogue
            {
                /// <summary>The route segment.</summary>
                public const string RouteSegment = "catalogue";

                /// <summary>The Swagger group name.</summary>
                public const string GroupName = "Catalogue";
            }
        }
    }

    /// <summary>
    /// Constants for the Automate Webhook API (public, no auth).
    /// </summary>
    public static class WebhookApi
    {
        /// <summary>
        /// The API name used for Swagger doc and JSON options.
        /// </summary>
        public const string ApiName = "automate-webhook";

        /// <summary>
        /// The API title.
        /// </summary>
        public const string ApiTitle = "Umbraco Automate Webhook API";
    }
}
