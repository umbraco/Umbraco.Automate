import { UMB_ACTION_EVENT_CONTEXT, type UmbActionEventContext } from "@umbraco-cms/backoffice/action";

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function dispatchActionEvent(host: any, event: Event) {
    (host.getContext(UMB_ACTION_EVENT_CONTEXT) as Promise<UmbActionEventContext | undefined>).then((context) => {
        context?.dispatchEvent(event);
    });
}

/**
 * A `keydown` handler that runs `activate` on Enter or Space, as a native button would. Pair it with
 * `role="button"` and `tabindex="0"` on a clickable element that is not a `<button>`, so keyboard
 * users can reach and trigger it too.
 */
export function onActivateKey(activate: () => void) {
    return (e: KeyboardEvent) => {
        if (e.key !== "Enter" && e.key !== " ") return;
        e.preventDefault(); // Space would otherwise scroll the page.
        activate();
    };
}
