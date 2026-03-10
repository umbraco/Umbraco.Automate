using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Notifications;

/// <summary>
/// Notification fired before an automation is saved. Can be cancelled.
/// </summary>
public sealed class AutomationSavingNotification(Automation target, EventMessages messages)
    : CancelableObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired after an automation has been saved.
/// </summary>
public sealed class AutomationSavedNotification(Automation target, EventMessages messages)
    : ObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired before an automation is published. Can be cancelled.
/// </summary>
public sealed class AutomationPublishingNotification(Automation target, EventMessages messages)
    : CancelableObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired after an automation has been published.
/// </summary>
public sealed class AutomationPublishedNotification(Automation target, EventMessages messages)
    : ObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired before an automation is unpublished. Can be cancelled.
/// </summary>
public sealed class AutomationUnpublishingNotification(Automation target, EventMessages messages)
    : CancelableObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired after an automation has been unpublished.
/// </summary>
public sealed class AutomationUnpublishedNotification(Automation target, EventMessages messages)
    : ObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired before an automation is deleted. Can be cancelled.
/// </summary>
public sealed class AutomationDeletingNotification(Automation target, EventMessages messages)
    : CancelableObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired after an automation has been deleted.
/// </summary>
public sealed class AutomationDeletedNotification(Automation target, EventMessages messages)
    : ObjectNotification<Automation>(target, messages);

/// <summary>
/// Notification fired before an automation run starts. Can be cancelled.
/// </summary>
public sealed class AutomationRunStartingNotification(AutomationRun target, EventMessages messages)
    : CancelableObjectNotification<AutomationRun>(target, messages);

/// <summary>
/// Notification fired after an automation run has completed (any status).
/// </summary>
public sealed class AutomationRunCompletedNotification(AutomationRun target, EventMessages messages)
    : ObjectNotification<AutomationRun>(target, messages);

/// <summary>
/// Notification fired after a step run has completed (any status).
/// </summary>
public sealed class StepRunCompletedNotification(StepRun target, EventMessages messages)
    : ObjectNotification<StepRun>(target, messages);
