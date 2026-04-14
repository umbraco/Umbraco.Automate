import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
} from "@umbraco-cms/backoffice/components";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaRunCollectionRepository } from "../../../../run/repository/collection/run-collection.repository.js";
import { UA_RUN_DETAIL_MODAL } from "../../../../run/modals/run-detail-modal.token.js";
import type { UaRunItemModel } from "../../../../run/types.js";
import { formatDateTime } from "../../../../core/index.js";

@customElement("ua-automation-runs-workspace-view")
export class UaAutomationRunsWorkspaceViewElement extends UmbLitElement {
    #runCollectionRepo: UaRunCollectionRepository;

    @state()
    private _tableConfig: UmbTableConfig = { allowSelection: false };

    @state()
    private _items: UmbTableItem[] = [];

    @state()
    private _loading = true;

    private _columns: UmbTableColumn[] = [
        { name: "Run", alias: "run" },
        { name: "Status", alias: "status" },
        { name: "Started", alias: "startedUtc" },
        { name: "Duration", alias: "duration" },
        { name: "Initiated By", alias: "initiatedBy" },
    ];

    constructor() {
        super();
        this.#runCollectionRepo = new UaRunCollectionRepository(this);

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.unique, (unique) => {
                if (unique) {
                    this.#loadRuns(unique);
                }
            });
        });
    }

    async #openRunModal(runId: string) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;
        modalManager.open(this, UA_RUN_DETAIL_MODAL, { data: { runId } });
    }

    async #loadRuns(automationId: string) {
        this._loading = true;
        const { data } = await this.#runCollectionRepo.requestCollection({
            automationId,
            skip: 0,
            take: 50,
        });

        if (data) {
            this.#createTableItems(data.items);
        }
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
                    columnAlias: "run",
                    value: html`<uui-button look="default" compact
                        label=${item.unique.substring(0, 8)}
                        @click=${() => this.#openRunModal(item.unique)}
                    >
                        ${item.unique.substring(0, 8)}...
                    </uui-button>`,
                },
                {
                    columnAlias: "status",
                    value: html`<div class="status-cell">
                        <uui-tag color=${this.#statusColor(item.status)} look="secondary">
                            ${item.status}
                        </uui-tag>
                        ${item.error
                            ? html`<span class="status-error">${item.error}</span>`
                            : ""}
                    </div>`,
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
                height: 100%;
                overflow-y: auto;
                box-sizing: border-box;
            }

            .center {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-3);
                color: var(--uui-color-text-alt);
            }

            .status-cell {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
            }

            .status-error {
                color: var(--uui-color-danger-standalone);
                font-size: var(--uui-size-4);
                overflow: hidden;
                text-overflow: ellipsis;
                white-space: nowrap;
                max-width: 300px;
            }

            .content {
                max-width: 1200px;
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
