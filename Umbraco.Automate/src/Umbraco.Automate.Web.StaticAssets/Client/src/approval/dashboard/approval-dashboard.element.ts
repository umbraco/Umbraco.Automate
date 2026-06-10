import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
} from "@umbraco-cms/backoffice/components";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { ApprovalsService } from "../../api/sdk.gen.js";
import type { PendingApprovalResponseModel } from "../../api/types.gen.js";
import { formatDateTime } from "../../core/index.js";
import { UA_APPROVAL_DECISION_MODAL } from "../modals/approval-decision/approval-decision-modal.token.js";

@customElement("ua-approval-dashboard")
export class UaApprovalDashboardElement extends UmbLitElement {
    @state()
    private _tableConfig: UmbTableConfig = { allowSelection: false };

    @state()
    private _items: UmbTableItem[] = [];

    @state()
    private _loading = true;

    #modalManager?: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE;

    private _columns: UmbTableColumn[] = [
        { name: this.localize.term("uaLabels_name"), alias: "automationName" },
        { name: this.localize.term("uaApproval_promptLabel"), alias: "prompt" },
        { name: this.localize.term("uaLabels_requestedAt"), alias: "requestedUtc" },
        { name: "", alias: "actions" },
    ];

    constructor() {
        super();
        this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (context) => {
            this.#modalManager = context;
        });
    }

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();
    }

    async #loadData() {
        this._loading = true;

        const { data } = await tryExecute(
            this,
            ApprovalsService.getApprovalsPending(),
        );

        if (data) {
            this.#createTableItems(data);
        }

        this._loading = false;
    }

    #createTableItems(items: PendingApprovalResponseModel[]) {
        this._items = items.map((item) => ({
            id: `${item.runId}_${item.stepId}`,
            icon: "icon-check",
            data: [
                {
                    columnAlias: "automationName",
                    value: item.automationName,
                },
                {
                    columnAlias: "prompt",
                    value: item.prompt ?? "-",
                },
                {
                    columnAlias: "requestedUtc",
                    value: item.requestedUtc ? formatDateTime(item.requestedUtc) : "-",
                },
                {
                    columnAlias: "actions",
                    value: html`
                        <div class="actions">
                            <uui-button
                                look="primary"
                                color="positive"
                                label=${this.localize.term("uaApproval_approve")}
                                @click=${() => this.#onDecision(item)}
                            ></uui-button>
                            <uui-button
                                look="primary"
                                color="danger"
                                label=${this.localize.term("uaApproval_reject")}
                                @click=${() => this.#onDecision(item)}
                            ></uui-button>
                        </div>
                    `,
                },
            ],
        }));
    }

    async #onDecision(item: PendingApprovalResponseModel) {
        if (!this.#modalManager) return;

        const modal = this.#modalManager.open(this, UA_APPROVAL_DECISION_MODAL, {
            data: {
                runId: item.runId,
                stepId: item.stepId,
                automationName: item.automationName,
                prompt: item.prompt ?? null,
            },
        });

        try {
            await modal.onSubmit();
            // Short delay to allow WorkflowCore to process the event and update step status
            await new Promise((resolve) => setTimeout(resolve, 1500));
            this.#loadData();
        } catch {
            // Modal was cancelled — do nothing
        }
    }

    override render() {
        if (this._loading) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        if (this._items.length === 0) {
            return html`
                <div class="center">
                    <p>${this.localize.term("uaApproval_noApprovals")}</p>
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

            .actions {
                display: flex;
                gap: var(--uui-size-space-2);
            }
        `,
    ];
}

export default UaApprovalDashboardElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-approval-dashboard": UaApprovalDashboardElement;
    }
}
