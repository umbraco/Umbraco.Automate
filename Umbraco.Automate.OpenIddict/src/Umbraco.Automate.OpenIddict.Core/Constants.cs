namespace Umbraco.Automate.OpenIddict;

/// <summary>
/// Constants for Umbraco Automate OpenIddict.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Well-known authentication property keys that providers can use to pass extra data
    /// from OpenIddict event handlers through to the callback controller.
    /// </summary>
    public static class OAuthProperties
    {
        /// <summary>
        /// A secondary access token (e.g. Slack V2 user token from <c>authed_user.access_token</c>).
        /// </summary>
        public const string UserAccessToken = "Automate.UserAccessToken";

        /// <summary>
        /// A display label for the authenticated account (e.g. workspace name).
        /// </summary>
        public const string AccountLabel = "Automate.AccountLabel";

        /// <summary>
        /// The granted scopes from the primary token (bot scopes).
        /// </summary>
        public const string Scopes = "Automate.Scopes";
    }

    /// <summary>
    /// Constants for the OAuth API.
    /// </summary>
    public static class OAuthApi
    {
        /// <summary>
        /// The API name used for Swagger doc grouping.
        /// </summary>
        public const string ApiName = "automate-oauth";

        /// <summary>
        /// The API title.
        /// </summary>
        public const string ApiTitle = "Umbraco Automate OAuth API";
    }
}
