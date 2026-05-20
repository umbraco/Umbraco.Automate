using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Per-automation context passed to <see cref="ITriggerDispatchAuthorizer"/> implementations.
/// </summary>
public sealed class TriggerDispatchAuthorizationContext
{
    /// <summary>
    /// The trigger that produced the event.
    /// </summary>
    public required ITrigger Trigger { get; init; }

    /// <summary>
    /// The deserialised trigger output, or <c>null</c> if the trigger has no declared output
    /// type or deserialisation failed. Authorisers that need the typed payload should
    /// pattern-match on a marker they recognise and return
    /// <see cref="Security.AutomationAuthorizationResult.Success"/> when the marker is absent
    /// (= the authoriser is not applicable to this event).
    /// </summary>
    public required object? TypedOutput { get; init; }

    /// <summary>
    /// The resolved workspace service account.
    /// </summary>
    public required IUser ServiceAccount { get; init; }

    /// <summary>
    /// The published automation that subscribed to the trigger.
    /// </summary>
    public required Automation Automation { get; init; }
}
