import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UaRunListCollectionServerDataSource } from "../repository/collection/run-list-collection.server.data-source.js";
import { UaRunsRefreshController } from "../controllers/runs-refresh.controller.js";
import type { UaRunItemModel } from "../types.js";

import "../components/runs-table/runs-table.element.js";

@customElement("ua-run-dashboard")
export class UaRunDashboardElement extends UmbLitElement {
    #runSource = new UaRunListCollectionServerDataSource(this);

    @state()
    private _items: UaRunItemModel[] = [];

    @state()
    private _loading = true;

    constructor() {
        super();
        // This dashboard aggregates runs across every automation, so it refreshes on any
        // manual run or replay (no automation-id filter).
        new UaRunsRefreshController(this, { reload: (quiet) => this.#loadData(quiet) });
    }

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();
    }

    async #loadData(quiet = false): Promise<UaRunItemModel[]> {
        if (!quiet) this._loading = true;

        // Single request: the endpoint returns the newest runs across all accessible
        // automations, already sorted and carrying each run's automation name.
        const { data, error } = await this.#runSource.getCollection({ skip: 0, take: 100 });

        // The data source surfaces an error notification; on failure keep the rows we
        // already have rather than collapsing to an empty "no runs" state.
        if (!error && data) {
            this._items = data.items;
        }

        this._loading = false;
        return this._items;
    }

    override render() {
        // Each item already carries its automationName, so no name map is needed.
        return html`<ua-runs-table
            .items=${this._items}
            .loading=${this._loading}
            ?show-automation-column=${true}
        ></ua-runs-table>`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
            }
        `,
    ];
}

export default UaRunDashboardElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-dashboard": UaRunDashboardElement;
    }
}
