import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT } from "@umbraco-cms/backoffice/management-api";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaRunCollectionRepository } from "../../../../run/repository/collection/run-collection.repository.js";
import { UA_RUN_EVENT_SOURCE, UA_RUN_EVENT_TYPES } from "../../../../run/constants.js";
import type { UaRunItemModel } from "../../../../run/types.js";

import "../../../../run/components/runs-table/runs-table.element.js";

@customElement("ua-automation-runs-workspace-view")
export class UaAutomationRunsWorkspaceViewElement extends UmbLitElement {
    #runCollectionRepo: UaRunCollectionRepository;

    @state()
    private _items: UaRunItemModel[] = [];

    @state()
    private _loading = true;

    #automationId?: string;

    constructor() {
        super();
        this.#runCollectionRepo = new UaRunCollectionRepository(this);

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.unique, (unique) => {
                if (unique) {
                    this.#automationId = unique;
                    this.#loadRuns(unique);
                }
            });
        });

        this.consumeContext(UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT, (context) => {
            if (!context) return;
            this.observe(
                context.byEventSourcesAndEventTypes([UA_RUN_EVENT_SOURCE], [...UA_RUN_EVENT_TYPES]),
                (event) => {
                    if (!event || !this.#automationId) return;
                    this.#scheduleReload();
                },
                "ua-automation-runs-server-events",
            );
        });
    }

    #reloadHandle?: ReturnType<typeof setTimeout>;
    #scheduleReload() {
        if (this.#reloadHandle !== undefined) return;
        this.#reloadHandle = setTimeout(() => {
            this.#reloadHandle = undefined;
            if (this.#automationId) this.#loadRuns(this.#automationId);
        }, 500);
    }

    async #loadRuns(automationId: string) {
        this._loading = true;
        const { data } = await this.#runCollectionRepo.requestCollection({
            automationId,
            skip: 0,
            take: 50,
        });

        this._items = data?.items ?? [];
        this._loading = false;
    }

    override render() {
        return html`<ua-runs-table
            .items=${this._items}
            .loading=${this._loading}
        ></ua-runs-table>`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
                height: 100%;
                overflow-y: auto;
                box-sizing: border-box;
            }
        `,
    ];
}

export default UaAutomationRunsWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-runs-workspace-view": UaAutomationRunsWorkspaceViewElement;
    }
}
