import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";
import { client } from "./api/client.gen.js";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { markCoreClientReady } from "./core/client-ready.js";
import { UaEditorNotificationListener } from "./core/realtime/editor-notification.listener.js";

// Re-export everything from the main index
export * from "./index.js";

// Public API since 18.5.1; the promise itself lives in its own module so other modules can share it.
export { coreClientReady } from "./core/client-ready.js";

let editorNotificationListener: UaEditorNotificationListener | undefined;

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
    _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
        if (!authContext) return;

        // Delegates to the auth context, which sets baseUrl and credentials (cookie auth,
        // no bearer token since Umbraco 19) and binds the default response interceptors
        // (401 retry, 403 handling, error normalization, server notifications). Replaces
        // the deprecated getOpenApiConfiguration()/setConfig() path.
        authContext.configureClient(client);

        markCoreClientReady();
    });

    editorNotificationListener = new UaEditorNotificationListener(_host);
};

export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
    editorNotificationListener?.destroy();
    editorNotificationListener = undefined;
};
