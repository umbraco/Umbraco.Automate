import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaRunsRefreshController } from "../../../../run/controllers/runs-refresh.controller.js";
import { UaRunCollectionRepository } from "../../../../run/repository/collection/run-collection.repository.js";
import type { UaRunItemModel } from "../../../../run/types.js";

import "../../../../run/components/runs-table/runs-table.element.js";

@customElement("ua-automation-runs-workspace-view")
export class UaAutomationRunsWorkspaceViewElement extends UmbLitElement {
    #runCollectionRepo: UaRunCollectionRepository;
    #automationId?: string;

    @state()
    private _items: UaRunItemModel[] = [];

    @state()
    private _loading = true;

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

        // Refresh the list after a manual run or replay of the automation in view.
        // The reload closure reads the current automation id, so it stays correct even
        // if the user navigates to another automation mid-poll.
        new UaRunsRefreshController(this, {
            reload: (quiet) =>
                this.#automationId ? this.#loadRuns(this.#automationId, quiet) : Promise.resolve([]),
            shouldHandle: (automationId) => automationId === this.#automationId,
        });
    }

    async #loadRuns(automationId: string, quiet = false): Promise<UaRunItemModel[]> {
        if (!quiet) this._loading = true;
        const { data } = await this.#runCollectionRepo.requestCollection({
            automationId,
            skip: 0,
            take: 50,
        });

        this._items = data?.items ?? [];
        this._loading = false;
        return this._items;
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
