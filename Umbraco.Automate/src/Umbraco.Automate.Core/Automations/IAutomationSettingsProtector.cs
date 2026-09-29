namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Encrypts and decrypts the sensitive settings held inside an automation — trigger settings
/// (including a webhook trigger's per-strategy authenticator settings), step settings and
/// notification channel settings.
/// </summary>
/// <remarks>
/// Sensitive fields are those marked <c>[Field(IsSensitive = true)]</c> on the owning trigger,
/// action, webhook authenticator or notification channel. The persisted definition and every
/// version snapshot go through this service, so the two cannot disagree about what is encrypted.
/// Encrypting is idempotent: values that are already encrypted are left as they are.
/// </remarks>
internal interface IAutomationSettingsProtector
{
    /// <summary>
    /// Returns the trigger with its sensitive settings encrypted, or the same instance if nothing needed encrypting.
    /// </summary>
    TriggerConfiguration? ProtectTrigger(TriggerConfiguration? trigger);

    /// <summary>
    /// Returns the step with its sensitive settings encrypted, or the same instance if nothing needed encrypting.
    /// </summary>
    StepConfiguration ProtectStep(StepConfiguration step);

    /// <summary>
    /// Returns the notification settings with each channel's sensitive settings encrypted.
    /// </summary>
    AutomationNotificationSettings? ProtectNotificationSettings(AutomationNotificationSettings? notificationSettings);

    /// <summary>
    /// Returns the trigger with any encrypted settings decrypted.
    /// </summary>
    TriggerConfiguration? UnprotectTrigger(TriggerConfiguration? trigger);

    /// <summary>
    /// Returns the step with any encrypted settings decrypted.
    /// </summary>
    StepConfiguration UnprotectStep(StepConfiguration step);

    /// <summary>
    /// Returns the notification settings with each channel's encrypted settings decrypted.
    /// </summary>
    AutomationNotificationSettings? UnprotectNotificationSettings(AutomationNotificationSettings? notificationSettings);

    /// <summary>
    /// Encrypts the sensitive settings of the automation's trigger, steps and notification channels in place.
    /// </summary>
    void ProtectAutomationSettings(Automation automation);

    /// <summary>
    /// Decrypts the encrypted settings of the automation's trigger, steps and notification channels in place.
    /// </summary>
    void UnprotectAutomationSettings(Automation automation);

    /// <summary>
    /// Gets the keys of the sensitive settings declared by the given owner, compared case-insensitively.
    /// Returns an empty set when the owner is not registered or declares no sensitive fields.
    /// </summary>
    IReadOnlySet<string> GetSensitiveKeys(SensitiveSettingsOwner owner, string alias);
}

/// <summary>
/// The kinds of registered type that declare a settings schema inside an automation.
/// </summary>
internal enum SensitiveSettingsOwner
{
    /// <summary>A trigger, looked up by trigger alias.</summary>
    Trigger,

    /// <summary>An action, looked up by action alias.</summary>
    Action,

    /// <summary>A webhook authenticator strategy, looked up by authenticator alias.</summary>
    WebhookAuthenticator,

    /// <summary>A notification channel, looked up by channel alias.</summary>
    NotificationChannel,
}
