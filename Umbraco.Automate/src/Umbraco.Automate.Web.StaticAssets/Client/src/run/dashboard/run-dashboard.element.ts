import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
} from "@umbraco-cms/backoffice/components";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UaAutomationCollectionServerDataSource } from "../../automation/repository/collection/automation-collection.server.data-source.js";
import { UaRunCollectionServerDataSource } from "../repository/collection/run-collection.server.data-source.js";
import { UA_RUN_DETAIL_MODAL } from "../modals/run-detail-modal.token.js";
import type { UaRunItemModel } from "../types.js";
import { formatDateTime } from "../../core/index.js";

@customElement("ua-run-dashboard")
export class UaRunDashboardElement extends UmbLitElement {
    #automationSource = new UaAutomationCollectionServerDataSource(this);
    #runSource = new UaRunCollectionServerDataSource(this);

    @state()
    private _tableConfig: UmbTableConfig = { allowSelection: false };

    @state()
    private _items: UmbTableItem[] = [];

    @state()
    private _loading = true;

    @state()
    private _automations: Map<string, string> = new Map();

    private _columns: UmbTableColumn[] = [
        { name: "Automation", alias: "automationName" },
        { name: "Status", alias: "status" },
        { name: "Started", alias: "startedUtc" },
        { name: "Duration", alias: "duration" },
        { name: "Initiated By", alias: "initiatedBy" },
    ];

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();
    }

    async #loadData() {
        this._loading = true;

        // Load automations for name lookup
        const { data: automationsData } = await this.#automationSource.getCollection({ skip: 0, take: 500 });
        if (automationsData) {
            this._automations = new Map(automationsData.items.map((a) => [a.unique, a.name]));
        }

        // Load runs from each automation
        const allRuns: UaRunItemModel[] = [];
        if (automationsData) {
            const runPromises = automationsData.items.map(async (a) => {
                const { data } = await this.#runSource.getCollection({ automationId: a.unique, skip: 0, take: 20 });
                if (data) {
                    return data.items.map((r) => {
                        r.automationName = a.name;
                        return r;
                    });
                }
                return [];
            });

            const results = await Promise.all(runPromises);
            for (const runs of results) {
                allRuns.push(...runs);
            }
        }

        // Sort by started date descending
        allRuns.sort((a, b) => {
            const aTime = a.startedUtc ? new Date(a.startedUtc).getTime() : 0;
            const bTime = b.startedUtc ? new Date(b.startedUtc).getTime() : 0;
            return bTime - aTime;
        });

        this.#createTableItems(allRuns.slice(0, 100));
        this._loading = false;
    }

    #statusColor(status: string): string {
        switch (status) {
            case "Completed":
                return "positive";
            case "Running":
            case "Pending":
                return "warning";
            case "Failed":
                return "danger";
            case "Cancelled":
            case "Suspended":
                return "default";
            default:
                return "default";
        }
    }

    #formatDuration(ms: number | null): string {
        if (ms == null) return "-";
        if (ms < 1000) return `${ms}ms`;
        const seconds = Math.floor(ms / 1000);
        if (seconds < 60) return `${seconds}s`;
        const minutes = Math.floor(seconds / 60);
        const remainingSeconds = seconds % 60;
        return `${minutes}m ${remainingSeconds}s`;
    }

    async #openRunModal(runId: string) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        modalManager.open(this, UA_RUN_DETAIL_MODAL, { data: { runId } });
    }

    #createTableItems(items: UaRunItemModel[]) {
        this._items = items.map((item) => ({
            id: item.unique,
            icon: "icon-nodes",
            data: [
                {
                    columnAlias: "automationName",
                    value: html`<uui-button look="default" compact
                        label=${item.automationName ?? item.automationId}
                        @click=${() => this.#openRunModal(item.unique)}
                    >
                        ${item.automationName ?? this._automations.get(item.automationId) ?? item.automationId}
                    </uui-button>`,
                },
                {
                    columnAlias: "status",
                    value: html`<uui-tag color=${this.#statusColor(item.status)} look="secondary">
                        ${item.status}
                    </uui-tag>`,
                },
                {
                    columnAlias: "startedUtc",
                    value: item.startedUtc ? formatDateTime(item.startedUtc) : "-",
                },
                {
                    columnAlias: "duration",
                    value: this.#formatDuration(item.durationMs),
                },
                {
                    columnAlias: "initiatedBy",
                    value: item.initiatedBy || "-",
                },
            ],
        }));
    }

    override render() {
        if (this._loading) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        if (this._items.length === 0) {
            return html`
                <div class="center">
                    <p>${this.localize.term("uaRun_noRuns")}</p>
                </div>
            `;
        }

        return html`
            <umb-table
                .config=${this._tableConfig}
                .columns=${this._columns}
                .items=${this._items}
            ></umb-table>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
            }

            .center {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-3);
                color: var(--uui-color-text-alt);
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
