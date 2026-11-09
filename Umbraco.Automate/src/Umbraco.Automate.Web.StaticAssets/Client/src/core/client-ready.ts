// Resolves once app.ts has configured the generated API client with auth. Lives in its own
// module so the entry file (app.ts) and early callers share one copy of the promise.
let resolveClientReady: (() => void) | undefined;

export const coreClientReady = new Promise<void>((resolve) => {
    resolveClientReady = resolve;
});

export function markCoreClientReady(): void {
    resolveClientReady?.();
    resolveClientReady = undefined;
}
