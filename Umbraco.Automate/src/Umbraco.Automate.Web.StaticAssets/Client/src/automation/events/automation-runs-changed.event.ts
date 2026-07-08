import { UmbEntityActionEvent } from "@umbraco-cms/backoffice/entity-action";
import { UA_AUTOMATION_ENTITY_TYPE } from "../constants.js";

/**
 * Fired on the action event bus when an automation's runs may have changed — i.e.
 * a manual run or a replay was started. Runs views subscribe via UaRunsRefreshController
 * (the per-automation runs workspace view and the cross-automation runs dashboard) and
 * re-fetch their lists.
 *
 * A dedicated event (rather than reusing a structural reload event) is used so only
 * the runs list refreshes. The triggered run is persisted (as Running) before its API
 * call returns, so it is already present when the view re-fetches; the view then polls
 * briefly to catch it transitioning to a terminal status.
 */
export class UaAutomationRunsChangedEvent extends UmbEntityActionEvent {
    static readonly TYPE = "ua:automation-runs-changed";

    constructor(automationId: string) {
        super(UaAutomationRunsChangedEvent.TYPE, {
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            unique: automationId,
        });
    }
}
