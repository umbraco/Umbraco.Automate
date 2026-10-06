import { css, html, customElement, state, nothing, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UA_RUN_WORKSPACE_CONTEXT } from "../run-workspace.context-token.js";
import type { UaRunDetailModel } from "../../../types.js";
import type { UaAutomationDetailModel } from "../../../../automation/types.js";
import type { CatalogueLookupEntry } from "../../../../automation/workspace/automation/canvas/types.js";
import { resolveDynamicStepOutcomes } from "../../../utils/run-outcomes.js";
import type { UaStepOutcome } from "../../../../catalogue/types.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import { formatDateTime, getRunStatusColor } from "../../../../core/index.js";
import "../../../components/step-run-detail/step-run-detail.element.js";
import { UA_RUN_TRIGGER_ROW_ID } from "../../../components/run-trigger-detail/run-trigger-detail.element.js";

@customElement("ua-run-details-view")
export class UaRunDetailsViewElement extends UmbLitElement {
    #catalogueRepository: UaCatalogueRepository;

    @state()
    private _run?: UaRunDetailModel;

    @state()
    private _expandedStep?: string;

    @state()
    private _actionNames = new Map<string, string>();

    /** Exits per step id: the action's static outcomes, or the resolved ones for a dynamic action. */
    @state()
    private _stepOutcomes = new Map<string, UaStepOutcome[]>();

    @state()
    private _triggerNames = new Map<string, string>();

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);
        this.consumeContext(UA_RUN_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.automation, (automation) => {
                this.#automation = automation;
                this.#loadStepOutcomes();
            });
            this.observe(context.run, (run) => {
                this._run = run;
                if (run) {
                    this.#loadCatalogueNames();
                    const firstFailed = run.stepRuns.find((sr) => sr.status === "Failed");
                    if (firstFailed) {
                        this._expandedStep = firstFailed.id;
                    }
                }
            });
        });
    }

    #automation?: UaAutomationDetailModel;

    /** Looks up each step's exits from its saved settings, so a step run can show its exit's label. */
    async #loadStepOutcomes() {
        const automation = this.#automation;
        if (!automation) return;

        const { data: actions } = await this.#catalogueRepository.requestActions();
        const catalogue = new Map<string, CatalogueLookupEntry>(
            (actions ?? []).map((a) => [
                a.alias,
                { name: a.name, outcomes: a.outcomes ?? [], hasDynamicOutcomes: a.hasDynamicOutcomes ?? false },
            ]),
        );
        const dynamic = await resolveDynamicStepOutcomes(this.#catalogueRepository, automation.steps, catalogue);
        if (automation !== this.#automation) return;

        this._stepOutcomes = new Map(
            automation.steps.map((step) => [
                step.id,
                dynamic.get(step.id) ?? catalogue.get(step.actionAlias)?.outcomes ?? [],
            ]),
        );
    }

    async #loadCatalogueNames() {
        const [{ data: actions }, { data: triggers }] = await Promise.all([
            this.#catalogueRepository.requestActions(),
            this.#catalogueRepository.requestTriggers(),
        ]);

        if (actions) {
            const names = new Map<string, string>();
            for (const a of actions) {
                names.set(a.alias, a.name);
            }
            this._actionNames = names;
        }

        if (triggers) {
            this._triggerNames = new Map(triggers.map((t) => [t.alias, t.name]));
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
                    <uui-box @ua-toggle-step=${this.#onToggleStep}>
                        <ua-run-trigger-detail
                            .runId=${this._run.unique}
                            .triggerName=${this._run.triggerAlias
                                ? (this._triggerNames.get(this._run.triggerAlias) ?? this._run.triggerAlias)
                                : ""}
                            .startedUtc=${this._run.startedUtc}
                            .expanded=${this._expandedStep === UA_RUN_TRIGGER_ROW_ID}
                        ></ua-run-trigger-detail>
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
                                          .runId=${this._run!.unique}
                                          .outcomes=${this._stepOutcomes.get(sr.stepId)}
                                      ></ua-step-run-detail>
                                  `,
                              )}
                    </uui-box>
                </div>
                <div class="sidebar">
                    <uui-box headline=${this.localize.term("uaLabels_runInfo")}>
                        <umb-property-layout label=${this.localize.term("uaLabels_status")} orientation="vertical">
                            <div slot="editor">
                                <uui-tag color=${getRunStatusColor(this._run.status)} look="secondary">
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

            .main > uui-box {
                --uui-box-default-padding: 0;
                overflow: hidden;
            }

            .main > uui-box > * + * {
                border-top: 1px solid var(--uui-color-border);
            }

            .error-output {
                background: var(--uui-color-danger-standalone);
                color: var(--uui-color-danger-contrast, white);
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
