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

    @state()
    private _error = false;

    #modalManager?: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE;

    /**
     * Approvals decided here that the server may still list as pending: the engine updates the step
     * only once it has processed the decision event, so a reload straight after can still return it.
     */
    #decidedIds = new Set<string>();

    private _columns: UmbTableColumn[] = [
        { name: this.localize.term("uaLabels_name"), alias: "automationName" },
        {
            name: this.localize.term("uaApproval_promptLabel"),
            alias: "prompt",
            // Prompts can contain long resolved binding values (e.g. an HTTP response body). Clip to a
            // single line here so rows stay compact; uui-table-cell shows the full text via its
            // auto-managed title attribute on hover. The decision modal is the authoritative place to
            // read the full prompt before approving/rejecting.
            clipText: true,
        },
        { name: this.localize.term("uaLabels_requestedAt"), alias: "requestedUtc" },
        // Shrink to the button's width rather than sharing the row equally with the text columns.
        { name: "", alias: "actions", align: "right", width: "1%" },
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

        const { data, error } = await tryExecute(
            this,
            ApprovalsService.getApprovalsPending(),
        );

        this._error = !!error;
        if (data) {
            // Once the server stops listing a decided approval, there is nothing left to hide.
            const pendingIds = new Set(data.map((item) => this.#itemId(item)));
            for (const id of this.#decidedIds) {
                if (!pendingIds.has(id)) this.#decidedIds.delete(id);
            }

            this.#createTableItems(data.filter((item) => !this.#decidedIds.has(this.#itemId(item))));
        }

        this._loading = false;
    }

    #itemId(item: PendingApprovalResponseModel) {
        return `${item.runId}_${item.stepId}`;
    }

    #createTableItems(items: PendingApprovalResponseModel[]) {
        this._items = items.map((item) => ({
            id: this.#itemId(item),
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
                    // One button: approve and reject both live in the decision modal, alongside the full prompt.
                    value: html`
                        <uui-button
                            look="primary"
                            label=${this.localize.term("uaApproval_review")}
                            @click=${() => this.#onDecision(item)}
                        ></uui-button>
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
        } catch {
            return; // Modal was closed without a decision.
        }

        // Drop the row now rather than waiting for the engine, and keep it hidden from any reload
        // that lands before the engine has caught up.
        const id = this.#itemId(item);
        this.#decidedIds.add(id);
        this._items = this._items.filter((row) => row.id !== id);
    }

    override render() {
        if (this._loading) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        if (this._error) {
            return html`
                <div class="center">
                    <p class="error">${this.localize.term("uaApproval_loadError")}</p>
                </div>
            `;
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

            .error {
                color: var(--uui-color-danger-standalone);
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
