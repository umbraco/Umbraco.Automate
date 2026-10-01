import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { ApprovalsService } from "../../../api/sdk.gen.js";
import type { UaApprovalDecisionModalData, UaApprovalDecisionModalValue } from "./types.js";

type Outcome = UaApprovalDecisionModalValue["outcome"];

@customElement("ua-approval-decision-modal")
export class UaApprovalDecisionModalElement extends UmbModalBaseElement<
    UaApprovalDecisionModalData,
    UaApprovalDecisionModalValue
> {
    @state()
    private _comment = "";

    @state()
    private _submitting = false;

    /** The button the user pressed, and how its request went — drives that button's `state`. */
    @state()
    private _decisionState?: { outcome: Outcome; state: "waiting" | "failed" };

    async #onDecision(outcome: Outcome) {
        if (!this.data || this._submitting) return;

        this._submitting = true;
        this._decisionState = { outcome, state: "waiting" };

        // throwOnError: tryExecute only raises its error notification for a rejected promise, and the
        // generated SDK client resolves 4xx/5xx responses normally by default — without it a failed
        // decision would only mark the button. The modal stays open so the user can retry.
        const { error } = await tryExecute(
            this,
            ApprovalsService.postApprovalsByRunIdStepsByStepIdDecision({
                path: { runId: this.data.runId, stepId: this.data.stepId },
                body: { outcome: outcome, comment: this._comment || undefined },
                throwOnError: true,
            }),
        );

        this._submitting = false;

        if (error) {
            this._decisionState = { outcome, state: "failed" };
            return;
        }

        this._decisionState = undefined;

        this.value = { outcome };
        this.modalContext?.submit();
    }

    #buttonState(outcome: Outcome) {
        return this._decisionState?.outcome === outcome ? this._decisionState.state : undefined;
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    #onCommentInput(event: InputEvent) {
        this._comment = (event.target as HTMLTextAreaElement).value;
    }

    override render() {
        if (!this.data) return html``;

        return html`
            <umb-body-layout .headline=${this.data.automationName}>
                <div id="content">
                    <uui-box>
                        ${this.data.prompt
                            ? html`
                                <umb-property-layout
                                    label=${this.localize.term("uaApproval_promptLabel")}
                                    orientation="vertical"
                                >
                                    <div slot="editor">
                                        <p class="prompt-text">${this.data.prompt}</p>
                                    </div>
                                </umb-property-layout>
                            `
                            : ""}

                        <umb-property-layout
                            label=${this.localize.term("uaApproval_comment")}
                            orientation="vertical"
                        >
                            <div slot="editor">
                                <uui-textarea
                                    id="comment"
                                    label=${this.localize.term("uaApproval_comment")}
                                    .value=${this._comment}
                                    placeholder=${this.localize.term("uaApproval_commentPlaceholder")}
                                    @input=${this.#onCommentInput}
                                ></uui-textarea>
                            </div>
                        </umb-property-layout>
                    </uui-box>
                </div>
                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onCancel}
                        .disabled=${this._submitting}
                    ></uui-button>
                    <uui-button
                        look="primary"
                        color="danger"
                        label=${this.localize.term("uaApproval_reject")}
                        @click=${() => this.#onDecision("Rejected")}
                        .state=${this.#buttonState("Rejected")}
                        .disabled=${this._submitting}
                    ></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.localize.term("uaApproval_approve")}
                        @click=${() => this.#onDecision("Approved")}
                        .state=${this.#buttonState("Approved")}
                        .disabled=${this._submitting}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            #content {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            umb-property-layout {
                --uui-size-layout-1: var(--uui-size-space-2);
            }

            .prompt-text {
                margin: 0;
                color: var(--uui-color-text);
                /* Resolved bindings can produce long, unbroken tokens (e.g. a raw JSON response body
                   with no spaces). Without this, such text overflows its container instead of
                   wrapping, making the rest of the prompt unreadable and unreachable. */
                overflow-wrap: break-word;
                word-break: break-word;
            }

            uui-textarea {
                width: 100%;
            }
        `,
    ];
}

export default UaApprovalDecisionModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-approval-decision-modal": UaApprovalDecisionModalElement;
    }
}
