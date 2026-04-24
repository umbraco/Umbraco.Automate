import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_SERVER_CONTEXT } from "@umbraco-cms/backoffice/server";
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationContext, type UmbNotificationColor } from "@umbraco-cms/backoffice/notification";
import { HubConnectionBuilder, type HubConnection } from "@umbraco-cms/backoffice/external/signalr";
import { EditorNotificationSeverity, type EditorNotificationMessage } from "./editor-notification.types.js";

const HUB_PATH = "/umbraco/automate/hubs/editor-notification";

/**
 * Subscribes to the server-side `EditorNotificationHub` once the backoffice user is
 * authenticated, and shows a toast whenever a matching content item is currently
 * being edited in the user's browser.
 */
export class UaEditorNotificationListener extends UmbControllerBase {
    #connection?: HubConnection;
    #authContext?: typeof UMB_AUTH_CONTEXT.TYPE;
    #serverContext?: typeof UMB_SERVER_CONTEXT.TYPE;
    #notificationContext?: UmbNotificationContext;

    constructor(host: UmbControllerHost) {
        super(host);

        this.consumeContext(UMB_AUTH_CONTEXT, (context) => {
            this.#authContext = context;
            this.#tryStart();
        });

        this.consumeContext(UMB_SERVER_CONTEXT, (context) => {
            this.#serverContext = context;
            this.#tryStart();
        });

        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
            this.#notificationContext = context;
        });
    }

    #tryStart() {
        if (!this.#authContext || !this.#serverContext) return;

        this.observe(this.#authContext.isAuthorized, (isAuthorized) => {
            if (isAuthorized === undefined) return;

            if (isAuthorized) {
                this.#startConnection();
            } else {
                this.#stopConnection();
            }
        });
    }

    async #startConnection() {
        if (this.#connection) return;

        const serverUrl = this.#serverContext?.getServerUrl();
        if (!serverUrl) return;

        this.#connection = new HubConnectionBuilder()
            // Cookie auth is used for same-origin SignalR connections — the redacted
            // token mirrors the pattern used by Umbraco's own server event hub.
            .withUrl(`${serverUrl}${HUB_PATH}`, { accessTokenFactory: () => "[redacted]" })
            .withAutomaticReconnect()
            .build();

        this.#connection.on("OnEditorNotification", (message: EditorNotificationMessage) => {
            this.#handleNotification(message);
        });

        try {
            await this.#connection.start();
        } catch (err) {
            console.warn("[Umbraco.Automate] Failed to connect to editor notification hub", err);
            this.#connection = undefined;
        }
    }

    async #stopConnection() {
        if (!this.#connection) return;
        try {
            await this.#connection.stop();
        } catch {
            // Disconnect errors aren't actionable — ignore.
        }
        this.#connection = undefined;
    }

    #handleNotification(message: EditorNotificationMessage) {
        if (!this.#notificationContext) return;
        if (!this.#isEditingContent(message.contentKey)) return;

        const color = severityToColor(message.severity);
        this.#notificationContext.peek(color, {
            data: {
                headline: message.title ?? undefined,
                message: message.message,
            },
        });
    }

    // The document workspace route includes the content key as a URL segment, e.g.
    // `/umbraco/section/content/workspace/document/edit/{key}`. Matching on the
    // pathname is robust enough without needing to consume the document workspace
    // context directly (which may not be available from this global scope).
    #isEditingContent(contentKey: string): boolean {
        const path = window.location.pathname.toLowerCase();
        return path.includes(contentKey.toLowerCase());
    }

    override destroy() {
        this.#stopConnection();
        super.destroy();
    }
}

function severityToColor(severity: EditorNotificationSeverity): UmbNotificationColor {
    switch (severity) {
        case EditorNotificationSeverity.Positive: return "positive";
        case EditorNotificationSeverity.Warning: return "warning";
        case EditorNotificationSeverity.Danger: return "danger";
        default: return "default";
    }
}
