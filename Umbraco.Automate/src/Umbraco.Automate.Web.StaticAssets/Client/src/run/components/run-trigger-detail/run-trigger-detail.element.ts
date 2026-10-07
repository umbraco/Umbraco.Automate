import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import type { PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaRunDataValueModel } from "../../types.js";
import { formatDateTime, onActivateKey } from "../../../core/index.js";
import { UaRunDetailServerDataSource } from "../../repository/detail/run-detail.server.data-source.js";
import "../run-data-block/run-data-block.element.js";

/** The id this row uses in the parent's shared expand/collapse state, alongside the step run ids. */
export const UA_RUN_TRIGGER_ROW_ID = "trigger";

/**
 * The first row of a run's step list: what the trigger passed in. Styled like
 * `<ua-step-run-detail>` so it reads as step zero, but a trigger has no step run, so the header
 * shows the trigger's name and when the run started instead of a duration and status. Expand/collapse is owned by the
 * parent, shared with the steps; the payload is fetched the first time the row is expanded, since
 * it can be large (a webhook body, say).
 */
@customElement("ua-run-trigger-detail")
export class UaRunTriggerDetailElement extends UmbLitElement {
    #dataSource = new UaRunDetailServerDataSource(this);

    /** The run whose trigger data is loaded (or loading), so it is fetched only once. */
    #loadedRunId?: string;

    @property({ attribute: "run-id" })
    runId = "";

    /** The trigger's display name. Falls back to a generic "Trigger" label when empty. */
    @property({ attribute: "trigger-name" })
    triggerName = "";

    @property({ attribute: false })
    startedUtc: string | null = null;

    @property({ type: Boolean })
    expanded = false;

    @state()
    private _data?: UaRunDataValueModel;

    @state()
    private _loading = false;

    @state()
    private _error = false;

    #toggle() {
        this.dispatchEvent(
            new CustomEvent("ua-toggle-step", { detail: { stepId: UA_RUN_TRIGGER_ROW_ID }, bubbles: true, composed: true }),
        );
    }

    protected override updated(changedProperties: PropertyValues<this>) {
        super.updated(changedProperties);
        if (this.expanded) {
            this.#load();
        }
    }

    async #load() {
        if (!this.runId || this.#loadedRunId === this.runId) return;

        const runId = this.runId;
        this.#loadedRunId = runId;
        this._data = undefined;
        this._loading = true;
        this._error = false;

        const { data, error } = await this.#dataSource.readTriggerData(runId);

        // A newer load started while this one was in flight; let that one win.
        if (this.#loadedRunId !== runId) return;

        this._loading = false;
        if (error || !data) {
            this._error = true;
            // Forget the run so re-expanding retries.
            this.#loadedRunId = undefined;
            return;
        }

        this._data = data;
    }

    #renderContent() {
        if (this._loading) {
            return html`<uui-loader-bar></uui-loader-bar>`;
        }

        if (this._error) {
            return html`<p class="data-error">${this.localize.term("uaRun_dataLoadFailed")}</p>`;
        }

        return html`
            <ua-run-data-block
                empty-text=${this.localize.term("uaRun_noTriggerData")}
                .data=${this._data}
            ></ua-run-data-block>
        `;
    }

    override render() {
        return html`
            <uui-box>
                <div
                    class="header"
                    role="button"
                    tabindex="0"
                    aria-expanded=${this.expanded ? "true" : "false"}
                    @click=${this.#toggle}
                    @keydown=${onActivateKey(() => this.#toggle())}
                >
                    <uui-icon name=${this.expanded ? "icon-navigation-down" : "icon-navigation-right"}></uui-icon>
                    <span class="name">${this.triggerName || this.localize.term("uaRun_trigger")}</span>
                    <span class="started">${this.startedUtc ? formatDateTime(this.startedUtc) : "-"}</span>
                </div>
                ${this.expanded ? html`<div class="content">${this.#renderContent()}</div>` : nothing}
            </uui-box>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            /* Sits flush inside the run's steps box, which provides the rounded corners. */
            uui-box {
                --uui-box-border-radius: 0;
                --uui-box-box-shadow: none;
                --uui-box-border-width: 0;
            }

            .header {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-3);
                /* Matches ua-step-run-detail's header, whose status tag makes it taller than this text. */
                min-height: var(--uui-size-14);
                box-sizing: border-box;
                cursor: pointer;
            }

            .header:hover {
                background: var(--uui-color-surface-alt);
            }

            .header:focus-visible {
                outline: 2px solid var(--uui-color-focus);
                outline-offset: -2px;
            }

            .name {
                flex: 1;
                font-weight: 500;
            }

            .started {
                color: var(--uui-color-text-alt);
                font-size: var(--uui-size-4);
            }

            .content {
                padding: var(--uui-size-space-5);
                border-top: 1px solid var(--uui-color-border);
            }

            .data-error {
                color: var(--uui-color-danger-standalone);
                margin: 0;
            }
        `,
    ];
}

export default UaRunTriggerDetailElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-trigger-detail": UaRunTriggerDetailElement;
    }
}
