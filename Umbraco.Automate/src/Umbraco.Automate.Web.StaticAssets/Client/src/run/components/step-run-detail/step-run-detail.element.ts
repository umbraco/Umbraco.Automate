import { css, html, customElement, property, state, nothing, repeat } from "@umbraco-cms/backoffice/external/lit";
import type { PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaStepRunDataModel, UaStepRunLogEntryModel, UaStepRunModel } from "../../types.js";
import { formatDateTime, formatLogTimestamp } from "../../../core/index.js";
import { UaRunDetailServerDataSource } from "../../repository/detail/run-detail.server.data-source.js";
import "../run-data-block/run-data-block.element.js";

type UaStepRunTab = "details" | "input" | "output" | "logs";

/**
 * Renders a single step run within a run's step list: a clickable header (status,
 * duration) that expands to show the error (always, when the step failed) above tabs for
 * Details (timestamps, retry count), Input, Output and Logs (the entries the action wrote). Expand/collapse state and the action
 * display name are owned by the parent (both `<ua-run-detail-modal>` and
 * `<ua-run-details-view>` track this across the whole step list), so this stays a controlled
 * component; the only thing it loads itself is the step's input/output, fetched the first
 * time the Input or Output tab is selected and cached. Log entries come with the step run.
 */
@customElement("ua-step-run-detail")
export class UaStepRunDetailElement extends UmbLitElement {
    #dataSource = new UaRunDetailServerDataSource(this);

    /** Identifies the step run whose data is loaded (or loading), so it is fetched only once. */
    #dataKey?: string;

    @property({ attribute: false })
    stepRun!: UaStepRunModel;

    @property()
    actionName = "";

    @property({ type: Boolean })
    expanded = false;

    /** The run this step run belongs to; needed to load the step's input and output. */
    @property({ attribute: "run-id" })
    runId = "";

    @state()
    private _data?: UaStepRunDataModel;

    @state()
    private _dataLoading = false;

    @state()
    private _dataError = false;

    @state()
    private _activeTab: UaStepRunTab = "details";

    /**
     * The tabs shown when the step is expanded, in order. To add one: add its id to
     * `UaStepRunTab`, an entry here and a case in `#renderTabPanel()`. Set `needsData` if its
     * panel shows the lazily loaded step data.
     */
    #tabs(): Array<{ id: UaStepRunTab; label: string; needsData?: boolean }> {
        return [
            { id: "details", label: this.localize.term("uaRun_details") },
            { id: "input", label: this.localize.term("uaRun_input"), needsData: true },
            { id: "output", label: this.localize.term("uaRun_output"), needsData: true },
            { id: "logs", label: `${this.localize.term("uaLabels_logs")} (${this.stepRun.logEntries.length})` },
        ];
    }

    #statusColor(status: string): string {
        switch (status) {
            case "Completed":
                return "positive";
            case "Running":
            case "Pending":
            case "WaitingForInput":
            case "Suspended":
            // A refused approval is not an error.
            case "Rejected":
                return "warning";
            case "Failed":
                return "danger";
            default:
                return "default";
        }
    }

    #logIcon(level: UaStepRunLogEntryModel["level"]): string {
        switch (level) {
            case "Error":
            case "Warning":
                return "icon-alert";
            default:
                return "icon-info";
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

    #toggle() {
        this.dispatchEvent(new CustomEvent("ua-toggle-step", { detail: { stepId: this.stepRun.id }, bubbles: true, composed: true }));
    }

    protected override updated(changedProperties: PropertyValues) {
        super.updated(changedProperties);
        // Only once a tab that shows the step's input/output is open, so expanding a step to
        // check its timings or error costs no request.
        if (this.expanded && this.#tabs().find((tab) => tab.id === this._activeTab)?.needsData) {
            this.#loadData();
        }
    }

    async #loadData() {
        if (!this.runId || !this.stepRun) return;

        // Keyed on status too: a step still running when first loaded has no output yet, so
        // re-fetch once the parent hands over a step run that has moved on.
        const key = `${this.runId}:${this.stepRun.id}:${this.stepRun.status}`;
        if (this.#dataKey === key) return;
        this.#dataKey = key;

        this._dataLoading = true;
        this._dataError = false;

        const { data, error } = await this.#dataSource.readStepRunData(this.runId, this.stepRun.id);

        // A newer load started while this one was in flight; let that one win.
        if (this.#dataKey !== key) return;

        this._dataLoading = false;
        if (error || !data) {
            this._dataError = true;
            // Forget the key so switching tabs or re-expanding retries.
            this.#dataKey = undefined;
            return;
        }

        this._data = data;
    }

    #renderDetails() {
        return html`
            <umb-property-layout label=${this.localize.term("uaLabels_started")} orientation="vertical">
                <div slot="editor">${this.stepRun.startedUtc ? formatDateTime(this.stepRun.startedUtc) : "-"}</div>
            </umb-property-layout>
            <umb-property-layout label=${this.localize.term("uaLabels_completed")} orientation="vertical">
                <div slot="editor">${this.stepRun.completedUtc ? formatDateTime(this.stepRun.completedUtc) : "-"}</div>
            </umb-property-layout>
            <umb-property-layout label=${this.localize.term("uaLabels_retryCount")} orientation="vertical">
                <div slot="editor">${this.stepRun.retryCount}</div>
            </umb-property-layout>
        `;
    }

    #renderError() {
        if (!this.stepRun.error) return nothing;

        return html`
            <div class="step-error">
                <umb-property-layout label=${this.localize.term("uaLabels_error")} orientation="vertical">
                    <div slot="editor">
                        <pre class="error-output">${this.stepRun.error}</pre>
                    </div>
                </umb-property-layout>
            </div>
        `;
    }

    #renderTabs() {
        return html`
            <uui-tab-group>
                ${repeat(
                    this.#tabs(),
                    (tab) => tab.id,
                    (tab) => html`
                        <uui-tab
                            .label=${tab.label}
                            ?active=${tab.id === this._activeTab}
                            @click=${() => (this._activeTab = tab.id)}
                        >
                            ${tab.label}
                        </uui-tab>
                    `,
                )}
            </uui-tab-group>
            <div class="tab-panel">${this.#renderTabPanel()}</div>
        `;
    }

    #renderTabPanel() {
        switch (this._activeTab) {
            case "details":
                return this.#renderDetails();
            case "input":
                return this.#renderData((data) =>
                    this.#renderDataBlock("uaRun_noInput", data.input),
                );
            case "output":
                return this.#renderData((data) =>
                    this.#renderDataBlock("uaRun_noOutput", data.output),
                );
            case "logs":
                return this.#renderLogEntries();
        }
    }

    #renderLogEntries() {
        if (this.stepRun.logEntries.length === 0) {
            return html`<p class="empty">${this.localize.term("uaRun_noLogEntries")}</p>`;
        }

        return html`
            <div class="log-list">
                ${repeat(
                    this.stepRun.logEntries,
                    (_entry, index) => index,
                    (entry) => html`
                        <div class="log-entry log-entry--${entry.level.toLowerCase()}">
                            <uui-icon name=${this.#logIcon(entry.level)}></uui-icon>
                            <span class="log-time">${formatLogTimestamp(entry.timestampUtc)}</span>
                            <span class="log-message">${entry.message}</span>
                        </div>
                    `,
                )}
            </div>
        `;
    }

    /** Shows a panel that needs the lazily loaded step data, with its loading and error states. */
    #renderData(renderLoaded: (data: UaStepRunDataModel) => unknown) {
        if (this._dataLoading && !this._data) {
            return html`<uui-loader-bar></uui-loader-bar>`;
        }

        if (this._dataError) {
            return html`<p class="data-error">${this.localize.term("uaRun_dataLoadFailed")}</p>`;
        }

        if (!this._data) return nothing;

        return renderLoaded(this._data);
    }

    #renderDataBlock(emptyKey: string, data: UaStepRunDataModel["input"]) {
        // No label: the selected tab already names the value.
        return html`<ua-run-data-block empty-text=${this.localize.term(emptyKey)} .data=${data}></ua-run-data-block>`;
    }

    override render() {
        const isExpanded = this.expanded;

        return html`
            <uui-box>
                <div class="step-header" @click=${this.#toggle}>
                    <uui-icon name=${isExpanded ? "icon-navigation-down" : "icon-navigation-right"}></uui-icon>
                    <span class="step-name">${this.actionName || this.stepRun.actionAlias}</span>
                    <span class="step-duration">${this.#formatDuration(this.stepRun.durationMs)}</span>
                    <uui-tag color=${this.#statusColor(this.stepRun.status)} look="secondary">
                        ${this.stepRun.status}
                    </uui-tag>
                </div>
                ${isExpanded
                    ? html`
                          <div class="step-details">
                              ${this.#renderError()}
                              ${this.#renderTabs()}
                          </div>
                      `
                    : nothing}
            </uui-box>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            /* Steps sit flush inside the run's steps box, which provides the rounded corners. */
            uui-box {
                --uui-box-border-radius: 0;
                --uui-box-box-shadow: none;
                --uui-box-border-width: 0;
            }

            .step-header {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-3);
                /* Matches ua-run-trigger-detail's header, which has no status tag to set its height. */
                min-height: var(--uui-size-14);
                box-sizing: border-box;
                cursor: pointer;
            }

            .step-header:hover {
                background: var(--uui-color-surface-alt);
            }

            .step-name {
                flex: 1;
                font-weight: 500;
            }

            .step-duration {
                color: var(--uui-color-text-alt);
                font-size: var(--uui-size-4);
            }

            .step-details {
                border-top: 1px solid var(--uui-color-border);
            }

            .step-error {
                padding: var(--uui-size-space-5) var(--uui-size-space-5) 0;
            }

            .error-output {
                background: var(--uui-color-danger-standalone);
                color: white;
                padding: var(--uui-size-space-3);
                border-radius: var(--uui-border-radius);
                font-size: var(--uui-size-4);
                overflow-x: auto;
                white-space: pre-wrap;
                word-break: break-all;
                margin: 0;
            }

            uui-tab-group {
                border-bottom: 1px solid var(--uui-color-border);
            }

            .tab-panel {
                padding: var(--uui-size-space-5);
            }

            .empty {
                color: var(--uui-color-text-alt);
                font-style: italic;
                margin: 0;
            }

            .log-list {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-2);
            }

            .log-entry {
                display: flex;
                align-items: baseline;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-2) var(--uui-size-space-3);
                border-radius: var(--uui-border-radius);
                font-size: var(--uui-size-4);
            }

            .log-entry uui-icon {
                flex-shrink: 0;
            }

            .log-time {
                flex-shrink: 0;
                color: var(--uui-color-text-alt);
                font-family: monospace;
            }

            .log-message {
                overflow-wrap: anywhere;
            }

            .log-entry--debug {
                color: var(--uui-color-text-alt);
                opacity: 0.75;
            }

            .log-entry--info {
                color: var(--uui-color-text-alt);
            }

            .log-entry--warning {
                color: var(--uui-color-warning-standalone);
            }

            .log-entry--error {
                color: var(--uui-color-danger-standalone);
            }

            .data-error {
                color: var(--uui-color-danger-standalone);
                margin: 0;
            }

            umb-property-layout[orientation="vertical"] {
                padding-bottom: 0;
            }

            umb-property-layout[orientation="vertical"]:first-of-type {
                padding-top: 0;
            }
        `,
    ];
}

export default UaStepRunDetailElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-step-run-detail": UaStepRunDetailElement;
    }
}
