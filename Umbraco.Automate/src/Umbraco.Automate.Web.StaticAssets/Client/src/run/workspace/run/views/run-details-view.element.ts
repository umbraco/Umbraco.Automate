import { css, html, customElement, state, nothing, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UA_RUN_WORKSPACE_CONTEXT } from "../run-workspace.context-token.js";
import type { UaRunDetailModel } from "../../../types.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import { formatDateTime } from "../../../../core/index.js";
import "../../../components/step-run-detail/step-run-detail.element.js";

@customElement("ua-run-details-view")
export class UaRunDetailsViewElement extends UmbLitElement {
    #catalogueRepository: UaCatalogueRepository;

    @state()
    private _run?: UaRunDetailModel;

    @state()
    private _expandedStep?: string;

    @state()
    private _actionNames = new Map<string, string>();

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);
        this.consumeContext(UA_RUN_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.run, (run) => {
                this._run = run;
                if (run) {
                    this.#loadActionNames();
                    const firstFailed = run.stepRuns.find((sr) => sr.status === "Failed");
                    if (firstFailed) {
                        this._expandedStep = firstFailed.id;
                    }
                }
            });
        });
    }

    async #loadActionNames() {
        const { data } = await this.#catalogueRepository.requestActions();
        if (!data) return;
        const names = new Map<string, string>();
        for (const a of data) {
            names.set(a.alias, a.name);
        }
        this._actionNames = names;
    }

    #statusColor(status: string): string {
        switch (status) {
            case "Completed":
                return "positive";
            case "Running":
            case "Pending":
            case "WaitingForInput":
                return "warning";
            case "Failed":
                return "danger";
            case "Skipped":
            case "Cancelled":
            case "Suspended":
                return "default";
            default:
                return "default";
        }
    }

    #onToggleStep(e: CustomEvent<{ stepId: string }>) {
        const stepId = e.detail.stepId;
        this._expandedStep = this._expandedStep === stepId ? undefined : stepId;
    }

    override render() {
        if (!this._run) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="layout">
                <div class="main">
                    <uui-box
                        headline=${this.localize.term("uaLabels_steps")}
                        @ua-toggle-step=${this.#onToggleStep}
                    >
                        ${this._run.stepRuns.length === 0
                            ? html`<p class="empty">${this.localize.term("uaRun_noStepRuns")}</p>`
                            : repeat(
                                  this._run.stepRuns,
                                  (sr) => sr.id,
                                  (sr) => html`
                                      <ua-step-run-detail
                                          .stepRun=${sr}
                                          .actionName=${this._actionNames.get(sr.actionAlias) ?? sr.actionAlias}
                                          .expanded=${this._expandedStep === sr.id}
                                      ></ua-step-run-detail>
                                  `,
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
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
                height: 100%;
                overflow-y: auto;
                box-sizing: border-box;
            }

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

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
                padding: var(--uui-size-layout-2);
            }

            umb-property-layout[orientation="vertical"] {
                padding-bottom: 0;
            }

            umb-property-layout:first-of-type {
                padding-top: 0;
            }

            uui-loader {
                display: block;
                margin: auto;
                position: absolute;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
            }
        `,
    ];
}

export default UaRunDetailsViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-details-view": UaRunDetailsViewElement;
    }
}
