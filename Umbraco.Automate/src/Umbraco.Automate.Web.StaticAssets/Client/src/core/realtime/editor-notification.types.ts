/**
 * Severity of an editor notification — mirrors the C# enum
 * `Umbraco.Automate.Core.Realtime.EditorNotificationSeverity` and maps to the
 * backoffice notification colour.
 */
export type EditorNotificationSeverity = "Default" | "Positive" | "Warning" | "Danger";

/**
 * Payload sent by the server over SignalR when an automation fires a
 * `Notify Editor` action.
 */
export interface EditorNotificationMessage {
    contentKey: string;
    contentName: string;
    message: string;
    severity: EditorNotificationSeverity;
}
