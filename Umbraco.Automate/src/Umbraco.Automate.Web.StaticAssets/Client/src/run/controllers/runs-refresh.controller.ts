import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UaAutomationRunsChangedEvent } from "../../automation/events/automation-runs-changed.event.js";
import type { UaRunItemModel } from "../types.js";

const POLL_ATTEMPTS = 6;
const POLL_INTERVAL_MS = 1500;

export interface UaRunsRefreshControllerArgs {
    /**
     * Reloads the runs and resolves with the current list. Called with `quiet = true`
     * while polling so the reload does not flash a loading spinner over existing rows.
     */
    reload: (quiet: boolean) => Promise<UaRunItemModel[]>;
    /**
     * Optional filter — return `false` to ignore an event for a given automation id.
     * Omit to react to every automation's runs (e.g. the cross-automation dashboard).
     */
    shouldHandle?: (automationId: string | null) => boolean;
}

/**
 * Listens for {@link UaAutomationRunsChangedEvent} on the action event bus and refreshes a
 * runs list when a manual run or replay is started. The triggered run is persisted (as
 * Running) before its API call returns, so it is already present on the first reload; this
 * then polls briefly to catch it transitioning to a terminal status.
 *
 * Best-effort: capped at ~7.5s (6 × 1500ms), after which a still-running run keeps showing
 * "Running" until the next reload.
 */
export class UaRunsRefreshController extends UmbControllerBase {
    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;
    #args: UaRunsRefreshControllerArgs;
    #refreshing = false;

    constructor(host: UmbControllerHost, args: UaRunsRefreshControllerArgs) {
        super(host);
        this.#args = args;

        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            this.#removeListener();
            this.#eventContext = context;
            this.#eventContext?.addEventListener(
                UaAutomationRunsChangedEvent.TYPE,
                this.#onRunsChanged as unknown as EventListener,
            );
        });
    }

    override hostDisconnected() {
        super.hostDisconnected();
        this.#removeListener();
    }

    override destroy() {
        this.#removeListener();
        super.destroy();
    }

    #removeListener() {
        this.#eventContext?.removeEventListener(
            UaAutomationRunsChangedEvent.TYPE,
            this.#onRunsChanged as unknown as EventListener,
        );
    }

    #onRunsChanged = (event: UaAutomationRunsChangedEvent) => {
        if (this.#args.shouldHandle && !this.#args.shouldHandle(event.getUnique())) return;
        this.#refresh();
    };

    async #refresh() {
        // A poll loop is already running — it will pick up the latest state.
        if (this.#refreshing) return;
        this.#refreshing = true;

        try {
            for (let attempt = 0; attempt < POLL_ATTEMPTS; attempt++) {
                if (attempt > 0) {
                    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
                }
                const items = await this.#args.reload(true);
                const inFlight = items.some(
                    (run) => run.status === "Pending" || run.status === "Running",
                );
                if (attempt > 0 && !inFlight) break;
            }
        } finally {
            this.#refreshing = false;
        }
    }
}
