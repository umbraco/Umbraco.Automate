import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";
import { client } from "./api/client.gen.js";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";

// Re-export everything from the main index
export * from "./index.js";

// Promise that resolves when the client is configured with auth
let coreClientReadyResolve: (() => void) | undefined;
export const coreClientReady = new Promise<void>((resolve) => {
    coreClientReadyResolve = resolve;
});

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
    _host.consumeContext(UMB_AUTH_CONTEXT, async (authContext) => {
        const config = authContext?.getOpenApiConfiguration();
        client.setConfig({
            auth: config?.token ?? undefined,
            baseUrl: config?.base ?? "",
            credentials: config?.credentials ?? "same-origin",
        });

        if (coreClientReadyResolve) {
            coreClientReadyResolve();
            coreClientReadyResolve = undefined;
        }
    });
};

export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
    // Clean up if needed
};
