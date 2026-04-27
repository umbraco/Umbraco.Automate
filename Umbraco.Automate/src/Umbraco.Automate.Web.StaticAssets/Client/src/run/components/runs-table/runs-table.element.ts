import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
} from "@umbraco-cms/backoffice/components";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import type { UaRunItemModel } from "../../types.js";
import { UA_RUN_DETAIL_MODAL } from "../../modals/run-detail-modal.token.js";
import { formatDateTime } from "../../../core/index.js";

/**
 * Shared run-list table used by the dashboard and the automation runs view.
 * Consumers provide the items; the table handles column rendering, modal
 * open-on-click, status colouring, and duration formatting.
 *
 * Set `show-automation-column` when displaying runs across multiple
 * automations — items must then carry `automationName` (or the consumer can
 * fill them via the optional `automationNames` map).
 */
@customElement("ua-runs-table")
export class UaRunsTableElement extends UmbLitElement {
    @property({ attribute: false })
    items: UaRunItemModel[] = [];

    @property({ type: Boolean, attribute: "show-automation-column" })
    showAutomationColumn = false;

    @property({ type: Boolean })
    loading = false;

    /**
     * Optional fallback for automation names when the item lacks one.
     */
    @property({ attribute: false })
    automationNames: Map<string, string> = new Map();

    @state()
    private _tableConfig: UmbTableConfig = { allowSelection: false, hideIcon: true };

    @state()
    private _tableItems: UmbTableItem[] = [];

    override updated(changed: Map<string, unknown>) {
        super.updated(changed);
        if (changed.has("items") || changed.has("showAutomationColumn") || changed.has("automationNames")) {
            this._tableItems = this.#buildTableItems();
        }
    }

    get #columns(): UmbTableColumn[] {
        const automation: UmbTableColumn = { name: "Automation", alias: "automationName" };
        const run: UmbTableColumn = { name: "Run", alias: "run" };
        const rest: UmbTableColumn[] = [
            { name: "Status", alias: "status" },
            { name: "Started", alias: "startedUtc" },
            { name: "Duration", alias: "duration" },
            { name: "Initiated By", alias: "initiatedBy" },
        ];
        return this.showAutomationColumn ? [automation, ...rest] : [run, ...rest];
    }

    async #openRunModal(runId: string) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        modalManager?.open(this, UA_RUN_DETAIL_MODAL, { data: { runId } });
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
        return `${minutes}m ${seconds % 60}s`;
    }

    #buildTableItems(): UmbTableItem[] {
        return this.items.map((item) => {
            const leadingCell = this.showAutomationColumn
                ? {
                      columnAlias: "automationName",
                      value: html`<uui-button
                          look="default"
                          compact
                          label=${item.automationName ?? item.automationId}
                          @click=${() => this.#openRunModal(item.unique)}
                      >
                          ${item.automationName
                              ?? this.automationNames.get(item.automationId)
                              ?? item.automationId}
                      </uui-button>`,
                  }
                : {
                      columnAlias: "run",
                      value: html`<uui-button
                          look="default"
                          compact
                          label=${item.unique.substring(0, 8)}
                          @click=${() => this.#openRunModal(item.unique)}
                      >
                          ${item.unique.substring(0, 8)}...
                      </uui-button>`,
                  };

            return {
                id: item.unique,
                data: [
                    leadingCell,
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
            };
        });
    }

    override render() {
        if (this.loading) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        if (this._tableItems.length === 0) {
            return html`<div class="center">
                <p>${this.localize.term("uaRun_noRuns")}</p>
            </div>`;
        }

        return html`<umb-table
            .config=${this._tableConfig}
            .columns=${this.#columns}
            .items=${this._tableItems}
        ></umb-table>`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
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
        `,
    ];
}

export default UaRunsTableElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-runs-table": UaRunsTableElement;
    }
}
