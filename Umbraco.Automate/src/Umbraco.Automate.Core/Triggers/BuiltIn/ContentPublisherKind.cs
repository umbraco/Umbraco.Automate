namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Classification of the user recorded as a content item's publisher, carried on
/// <see cref="ContentPublishedTriggerOutput.PublisherKind"/>.
/// </summary>
public static class ContentPublisherKind
{
    /// <summary>
    /// A regular back-office user — a person publishing through the backoffice.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// An API user (<c>UserKind.Api</c>) — e.g. an automation service account or a
    /// headless/integration client.
    /// </summary>
    public const string Api = "api";

    /// <summary>
    /// The super user (id -1) — how scheduled publishing and in-process code publish
    /// when no explicit user is supplied.
    /// </summary>
    public const string System = "system";
}
