import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
} from "@umbraco-cms/backoffice/components";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../type-mapper.js";
import type { UaRunItemModel } from "../types.js";
import { formatDateTime } from "../../core/index.js";
import { UA_RUN_WORKSPACE_PATH } from "../workspace/run/paths.js";

@customElement("ua-run-dashboard")
export class UaRunDashboardElement extends UmbLitElement {
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
        const { data: automationsData } = await tryExecute(
            this,
            AutomationsService.getAutomations({ query: { skip: 0, take: 500 } }),
        );
        if (automationsData) {
            this._automations = new Map(automationsData.items.map((a) => [a.id, a.name]));
        }

        // Load runs from each automation
        const allRuns: UaRunItemModel[] = [];
        if (automationsData) {
            const runPromises = automationsData.items.map(async (a) => {
                const { data } = await tryExecute(
                    this,
                    AutomationsService.getAutomationsByIdRuns({
                        path: { id: a.id },
                        query: { skip: 0, take: 20 },
                    }),
                );
                if (data) {
                    return data.items.map((r) => {
                        const item = UaRunTypeMapper.toItemModel(r);
                        item.automationName = a.name;
                        return item;
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

    #createTableItems(items: UaRunItemModel[]) {
        this._items = items.map((item) => ({
            id: item.unique,
            icon: "icon-nodes",
            data: [
                {
                    columnAlias: "automationName",
                    value: html`<a href="${UA_RUN_WORKSPACE_PATH}/edit/${item.unique}">
                        ${item.automationName ?? this._automations.get(item.automationId) ?? item.automationId}
                    </a>`,
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
