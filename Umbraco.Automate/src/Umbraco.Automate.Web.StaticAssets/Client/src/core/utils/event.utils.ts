import { UMB_ACTION_EVENT_CONTEXT, type UmbActionEventContext } from "@umbraco-cms/backoffice/action";

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function dispatchActionEvent(host: any, event: Event) {
    (host.getContext(UMB_ACTION_EVENT_CONTEXT) as Promise<UmbActionEventContext | undefined>).then((context) => {
        context?.dispatchEvent(event);
    });
}
