import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT } from "@umbraco-cms/backoffice/management-api";
import { UaAutomationCollectionServerDataSource } from "../../automation/repository/collection/automation-collection.server.data-source.js";
import { UaRunCollectionServerDataSource } from "../repository/collection/run-collection.server.data-source.js";
import { UA_RUN_EVENT_SOURCE, UA_RUN_EVENT_TYPES } from "../constants.js";
import type { UaRunItemModel } from "../types.js";

import "../components/runs-table/runs-table.element.js";

@customElement("ua-run-dashboard")
export class UaRunDashboardElement extends UmbLitElement {
    #automationSource = new UaAutomationCollectionServerDataSource(this);
    #runSource = new UaRunCollectionServerDataSource(this);

    @state()
    private _items: UaRunItemModel[] = [];

    @state()
    private _automationNames = new Map<string, string>();

    @state()
    private _loading = true;

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();

        this.consumeContext(UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT, (context) => {
            if (!context) return;
            this.observe(
                context.byEventSourcesAndEventTypes([UA_RUN_EVENT_SOURCE], [...UA_RUN_EVENT_TYPES]),
                (event) => {
                    if (!event) return;
                    this.#scheduleReload();
                },
                "ua-run-dashboard-server-events",
            );
        });
    }

    #reloadHandle?: ReturnType<typeof setTimeout>;
    /**
     * Coalesce server events that arrive in bursts (e.g. start + complete in quick
     * succession) into a single reload so we don't hammer the server.
     */
    #scheduleReload() {
        if (this.#reloadHandle !== undefined) return;
        this.#reloadHandle = setTimeout(() => {
            this.#reloadHandle = undefined;
            this.#loadData();
        }, 500);
    }

    async #loadData() {
        this._loading = true;

        const { data: automationsData } = await this.#automationSource.getCollection({ skip: 0, take: 500 });
        if (!automationsData) {
            this._items = [];
            this._loading = false;
            return;
        }

        this._automationNames = new Map(automationsData.items.map((a) => [a.unique, a.name]));

        const runResults = await Promise.all(
            automationsData.items.map(async (a) => {
                const { data } = await this.#runSource.getCollection({ automationId: a.unique, skip: 0, take: 20 });
                return data?.items.map((r) => ({ ...r, automationName: a.name })) ?? [];
            }),
        );

        const allRuns = runResults.flat().sort((a, b) => {
            const aTime = a.startedUtc ? new Date(a.startedUtc).getTime() : 0;
            const bTime = b.startedUtc ? new Date(b.startedUtc).getTime() : 0;
            return bTime - aTime;
        });

        this._items = allRuns.slice(0, 100);
        this._loading = false;
    }

    override render() {
        return html`<ua-runs-table
            .items=${this._items}
            .loading=${this._loading}
            .automationNames=${this._automationNames}
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
