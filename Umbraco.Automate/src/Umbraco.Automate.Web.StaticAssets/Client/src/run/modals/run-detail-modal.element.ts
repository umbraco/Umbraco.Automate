import { css, html, customElement, state, nothing, repeat, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UaRunDetailServerDataSource } from "../repository/detail/run-detail.server.data-source.js";
import { UaCatalogueRepository } from "../../catalogue/repository/catalogue.repository.js";
import { formatDateTime } from "../../core/index.js";
import { client } from "../../api/client.gen.js";
import type { UaRunDetailModel, UaStepRunModel } from "../types.js";
import type { UaRunDetailModalData } from "./run-detail-modal.token.js";

@customElement("ua-run-detail-modal")
export class UaRunDetailModalElement extends UmbModalBaseElement<UaRunDetailModalData> {
    #dataSource = new UaRunDetailServerDataSource(this);
    #catalogueRepository = new UaCatalogueRepository(this);

    @state()
    private _run?: UaRunDetailModel;

    @state()
    private _loading = true;

    @state()
    private _replaying = false;

    @state()
    private _lifecycleBusy = false;

    @state()
    private _expandedStep?: string;

    @state()
    private _actionNames = new Map<string, string>();

    override connectedCallback() {
        super.connectedCallback();
        if (this.data?.runId) {
            this.#loadRun(this.data.runId);
        }
    }

    async #loadRun(runId: string) {
        this._loading = true;

        const [{ data: run }, { data: actions }] = await Promise.all([
            this.#dataSource.read(runId),
            this.#catalogueRepository.requestActions(),
        ]);

        if (run) {
            this._run = run;
            const firstFailed = run.stepRuns.find((sr) => sr.status === "Failed");
            if (firstFailed) {
                this._expandedStep = firstFailed.id;
            }
        }

        if (actions) {
            const names = new Map<string, string>();
            for (const a of actions) {
                names.set(a.alias, a.name);
            }
            this._actionNames = names;
        }

        this._loading = false;
    }

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

    async #onReplay() {
        if (!this._run) return;
        this._replaying = true;
        try {
            await client.post({
                url: `/umbraco/automate/management/api/v1/runs/${this._run.unique}/replay`,
                security: [{ scheme: "bearer", type: "http" }],
            });
            this.modalContext?.submit();
        } catch {
            // Replay failed — stay on modal
        } finally {
            this._replaying = false;
        }
    }

    async #callLifecycle(action: "suspend" | "resume" | "terminate") {
        if (!this._run) return;
        this._lifecycleBusy = true;
        try {
            await client.post({
                url: `/umbraco/automate/management/api/v1/runs/${this._run.unique}/${action}`,
                security: [{ scheme: "bearer", type: "http" }],
            });
            // Reload the run so the user sees the updated status without closing the modal.
            await this.#loadRun(this._run.unique);
        } catch {
            // Lifecycle call failed — stay on modal with current state.
        } finally {
            this._lifecycleBusy = false;
        }
    }

    #toggleStep(stepId: string) {
        this._expandedStep = this._expandedStep === stepId ? undefined : stepId;
    }

    #renderStepRun(stepRun: UaStepRunModel) {
        const isExpanded = this._expandedStep === stepRun.id;

        return html`
            <uui-box>
                <div class="step-header" @click=${() => this.#toggleStep(stepRun.id)}>
                    <uui-icon name=${isExpanded ? "icon-navigation-down" : "icon-navigation-right"}></uui-icon>
                    <span class="step-name">${this._actionNames.get(stepRun.actionAlias) ?? stepRun.actionAlias}</span>
                    <span class="step-duration">${this.#formatDuration(stepRun.durationMs)}</span>
                    <uui-tag color=${this.#statusColor(stepRun.status)} look="secondary">
                        ${stepRun.status}
                    </uui-tag>
                </div>
                ${isExpanded
                    ? html`
                          <div class="step-details">
                              <umb-property-layout label=${this.localize.term("uaLabels_started")} orientation="vertical">
                                  <div slot="editor">
                                      ${stepRun.startedUtc ? formatDateTime(stepRun.startedUtc) : "-"}
                                  </div>
                              </umb-property-layout>
                              <umb-property-layout label=${this.localize.term("uaLabels_completed")} orientation="vertical">
                                  <div slot="editor">
                                      ${stepRun.completedUtc ? formatDateTime(stepRun.completedUtc) : "-"}
                                  </div>
                              </umb-property-layout>
                              <umb-property-layout label=${this.localize.term("uaLabels_retryCount")} orientation="vertical">
                                  <div slot="editor">${stepRun.retryCount}</div>
                              </umb-property-layout>
                              ${stepRun.error
                                  ? html`
                                        <umb-property-layout label=${this.localize.term("uaLabels_error")} orientation="vertical">
                                            <div slot="editor">
                                                <pre class="error-output">${stepRun.error}</pre>
                                            </div>
                                        </umb-property-layout>
                                    `
                                  : nothing}
                          </div>
                      `
                    : nothing}
            </uui-box>
        `;
    }

    override render() {
        const headline = this._run
            ? `Run ${this._run.unique.substring(0, 8)}...`
            : this.localize.term("uaLabels_runInfo");

        return html`
            <umb-body-layout .headline=${headline}>
                ${this._loading
                    ? html`<div class="center"><uui-loader></uui-loader></div>`
                    : this._run
                      ? this.#renderContent()
                      : html`<p class="center">${this.localize.term("uaRun_noRuns")}</p>`}

                <div slot="actions">
                    ${when(
                        this._run?.status === "Running",
                        () => html`
                            <uui-button
                                label=${this.localize.term("uaRun_suspend")}
                                ?state=${this._lifecycleBusy ? "waiting" : undefined}
                                ?disabled=${this._lifecycleBusy}
                                @click=${() => this.#callLifecycle("suspend")}
                            >
                                <uui-icon name="icon-pause"></uui-icon>
                                ${this.localize.term("uaRun_suspend")}
                            </uui-button>
                        `,
                    )}
                    ${when(
                        this._run?.status === "Suspended",
                        () => html`
                            <uui-button
                                look="primary"
                                label=${this.localize.term("uaRun_resume")}
                                ?state=${this._lifecycleBusy ? "waiting" : undefined}
                                ?disabled=${this._lifecycleBusy}
                                @click=${() => this.#callLifecycle("resume")}
                            >
                                <uui-icon name="icon-play"></uui-icon>
                                ${this.localize.term("uaRun_resume")}
                            </uui-button>
                        `,
                    )}
                    ${when(
                        this._run?.status === "Running" || this._run?.status === "Suspended",
                        () => html`
                            <uui-button
                                color="danger"
                                label=${this.localize.term("uaRun_terminate")}
                                ?state=${this._lifecycleBusy ? "waiting" : undefined}
                                ?disabled=${this._lifecycleBusy}
                                @click=${() => this.#callLifecycle("terminate")}
                            >
                                <uui-icon name="icon-stop-alt"></uui-icon>
                                ${this.localize.term("uaRun_terminate")}
                            </uui-button>
                        `,
                    )}
                    ${when(
                        this._run && (this._run.status === "Failed" || this._run.status === "Completed"),
                        () => html`
                            <uui-button
                                look="primary"
                                label=${this.localize.term("uaRun_replay")}
                                ?state=${this._replaying ? "waiting" : undefined}
                                ?disabled=${this._replaying}
                                @click=${this.#onReplay}
                            >
                                <uui-icon name="icon-redo"></uui-icon>
                                ${this.localize.term("uaRun_replay")}
                            </uui-button>
                        `,
                    )}
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${() => this.modalContext?.reject()}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    #renderContent() {
        if (!this._run) return nothing;

        return html`
            <div class="layout">
                <div class="main">
                    <uui-box headline=${this.localize.term("uaLabels_steps")}>
                        ${this._run.stepRuns.length === 0
                            ? html`<p class="empty">${this.localize.term("uaRun_noStepRuns")}</p>`
                            : repeat(
                                  this._run.stepRuns,
                                  (sr) => sr.id,
                                  (sr) => this.#renderStepRun(sr),
                              )}
                    </uui-box>
                </div>
                <div class="sidebar">
                    <uui-box headline=${this.localize.term("uaLabels_runInfo")}>
                        <umb-property-layout label=${this.localize.term("uaLabels_status")} orientation="vertical">
                            <div slot="editor">
                                <uui-tag color=${this.#statusColor(this._run.status)} look="secondary">
                                    ${this._run.status}
                                </uui-tag>
                            </div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_started")} orientation="vertical">
                            <div slot="editor">
                                ${this._run.startedUtc ? formatDateTime(this._run.startedUtc) : "-"}
                            </div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_completed")} orientation="vertical">
                            <div slot="editor">
                                ${this._run.completedUtc ? formatDateTime(this._run.completedUtc) : "-"}
                            </div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_initiatedBy")} orientation="vertical">
                            <div slot="editor">${this._run.initiatedBy || "-"}</div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_automationVersion")} orientation="vertical">
                            <div slot="editor">${this._run.automationVersion}</div>
                        </umb-property-layout>
                        ${this._run.correlationId
                            ? html`
                                  <umb-property-layout label=${this.localize.term("uaLabels_correlationId")} orientation="vertical">
                                      <div slot="editor">${this._run.correlationId}</div>
                                  </umb-property-layout>
                              `
                            : nothing}
                        ${this._run.error
                            ? html`
                                  <umb-property-layout label=${this.localize.term("uaLabels_error")} orientation="vertical">
                                      <div slot="editor">
                                          <pre class="error-output">${this._run.error}</pre>
                                      </div>
                                  </umb-property-layout>
                              `
                            : nothing}
                    </uui-box>
                </div>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            .layout {
                display: grid;
                gap: var(--uui-size-layout-1);
                grid-template-columns: 1fr 350px;
            }

            .main,
            .sidebar {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            .main uui-box {
                --uui-box-default-padding: 0;
                --uui-box-border-radius: 0;
            }

            .main uui-box > uui-box {
                --uui-box-box-shadow: 0;
            }

            .main uui-box > uui-box + uui-box {
                border-top: 1px solid var(--uui-color-border);
            }

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

            .center {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-3);
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
                padding: var(--uui-size-layout-2);
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

export default UaRunDetailModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-detail-modal": UaRunDetailModalElement;
    }
}
