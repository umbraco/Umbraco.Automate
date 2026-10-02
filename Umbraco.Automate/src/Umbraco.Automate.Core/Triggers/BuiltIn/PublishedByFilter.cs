namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Values for the "Published by" trigger setting — restricts which kind of publishing
/// user fires the trigger.
/// </summary>
public enum PublishedByFilter
{
    /// <summary>
    /// Fire regardless of who performed the publish.
    /// </summary>
    Anyone = 0,

    /// <summary>
    /// Only fire when a back-office (human) user performed the publish.
    /// </summary>
    User = 1,

    /// <summary>
    /// Only fire when the publish had no person behind it — the super user (scheduled
    /// publishing, in-process code) or an API user such as an automation service account.
    /// </summary>
    System = 2,
}
