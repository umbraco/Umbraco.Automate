import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";
import { client } from "./api/client.gen.js";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UaEditorNotificationListener } from "./core/realtime/editor-notification.listener.js";

// Re-export everything from the main index
export * from "./index.js";

// Promise that resolves when the client is configured with auth
let coreClientReadyResolve: (() => void) | undefined;
export const coreClientReady = new Promise<void>((resolve) => {
    coreClientReadyResolve = resolve;
});

let editorNotificationListener: UaEditorNotificationListener | undefined;

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
    _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
        if (!authContext) return;

        // Delegates to the auth context, which sets baseUrl, credentials, and a
        // per-request auth callback (inline refresh + cross-tab Web Lock
        // coordination), and binds the default response interceptors (401 retry,
        // 403 handling, error normalization, server notifications). Replaces the
        // deprecated getOpenApiConfiguration()/setConfig() path.
        authContext.configureClient(client);

        if (coreClientReadyResolve) {
            coreClientReadyResolve();
            coreClientReadyResolve = undefined;
        }
    });

    editorNotificationListener = new UaEditorNotificationListener(_host);
};

export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
    editorNotificationListener?.destroy();
    editorNotificationListener = undefined;
};
