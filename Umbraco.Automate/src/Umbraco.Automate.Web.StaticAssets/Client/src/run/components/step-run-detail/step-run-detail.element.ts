import { css, html, customElement, property, nothing, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaStepRunLogEntryModel, UaStepRunModel } from "../../types.js";
import { formatDateTime, formatLogTimestamp } from "../../../core/index.js";

/**
 * Renders a single step run within a run's step list: a clickable header (status,
 * duration) that expands to show timestamps, retry count, error, and log entries.
 * Expand/collapse state and the action display name are owned by the parent (both
 * `<ua-run-detail-modal>` and `<ua-run-details-view>` track this across the whole
 * step list), so this stays a controlled, presentation-only component.
 */
@customElement("ua-step-run-detail")
export class UaStepRunDetailElement extends UmbLitElement {
    @property({ attribute: false })
    stepRun!: UaStepRunModel;

    @property()
    actionName = "";

    @property({ type: Boolean })
    expanded = false;

    #statusColor(status: string): string {
        switch (status) {
            case "Completed":
                return "positive";
            case "Running":
            case "Pending":
            case "WaitingForInput":
            case "Suspended":
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

    #logIcon(level: UaStepRunLogEntryModel["level"]): string {
        switch (level) {
            case "Error":
                return "icon-alert";
            case "Warning":
                return "icon-alert";
            default:
                return "icon-info";
        }
    }

    #toggle() {
        this.dispatchEvent(new CustomEvent("ua-toggle-step", { detail: { stepId: this.stepRun.id }, bubbles: true, composed: true }));
    }

    #renderLogEntries() {
        if (this.stepRun.logEntries.length === 0) return nothing;

        return html`
            <umb-property-layout label=${this.localize.term("uaLabels_logs")} orientation="vertical">
                <div slot="editor" class="log-list">
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
            </umb-property-layout>
        `;
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
                              <umb-property-layout label=${this.localize.term("uaLabels_started")} orientation="vertical">
                                  <div slot="editor">
                                      ${this.stepRun.startedUtc ? formatDateTime(this.stepRun.startedUtc) : "-"}
                                  </div>
                              </umb-property-layout>
                              <umb-property-layout label=${this.localize.term("uaLabels_completed")} orientation="vertical">
                                  <div slot="editor">
                                      ${this.stepRun.completedUtc ? formatDateTime(this.stepRun.completedUtc) : "-"}
                                  </div>
                              </umb-property-layout>
                              <umb-property-layout label=${this.localize.term("uaLabels_retryCount")} orientation="vertical">
                                  <div slot="editor">${this.stepRun.retryCount}</div>
                              </umb-property-layout>
                              ${this.stepRun.error
                                  ? html`
                                        <umb-property-layout label=${this.localize.term("uaLabels_error")} orientation="vertical">
                                            <div slot="editor">
                                                <pre class="error-output">${this.stepRun.error}</pre>
                                            </div>
                                        </umb-property-layout>
                                    `
                                  : nothing}
                              ${this.#renderLogEntries()}
                          </div>
                      `
                    : nothing}
            </uui-box>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            .step-header {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-3);
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
                padding: var(--uui-size-space-5);
                border-top: 1px solid var(--uui-color-border);
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
