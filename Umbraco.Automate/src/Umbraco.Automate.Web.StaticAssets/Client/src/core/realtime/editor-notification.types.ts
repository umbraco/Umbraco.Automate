/**
 * Severity of an editor notification — mirrors the C# enum
 * `Umbraco.Automate.Core.Realtime.EditorNotificationSeverity` and maps to the
 * backoffice notification colour.
 *
 * Values are numeric because the SignalR JSON protocol serialises .NET enums as
 * their underlying integer value by default, and we deliberately avoid reconfiguring
 * the global protocol options (doing so would affect other hubs like Umbraco Deploy).
 */
export const EditorNotificationSeverity = {
    Default: 0,
    Positive: 1,
    Warning: 2,
    Danger: 3,
} as const;

export type EditorNotificationSeverity =
    (typeof EditorNotificationSeverity)[keyof typeof EditorNotificationSeverity];

/**
 * Payload sent by the server over SignalR when an automation fires a
 * `Notify Editor` action.
 */
export interface EditorNotificationMessage {
    contentKey: string;
    title?: string | null;
    message: string;
    severity: EditorNotificationSeverity;
}
