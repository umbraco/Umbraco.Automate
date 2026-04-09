import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { ApprovalsService } from "../../../api/sdk.gen.js";
import type { UaApprovalDecisionModalData, UaApprovalDecisionModalValue } from "./types.js";

@customElement("ua-approval-decision-modal")
export class UaApprovalDecisionModalElement extends UmbModalBaseElement<
    UaApprovalDecisionModalData,
    UaApprovalDecisionModalValue
> {
    @state()
    private _comment = "";

    @state()
    private _submitting = false;

    async #onDecision(outcome: "Approved" | "Rejected") {
        if (!this.data || this._submitting) return;

        this._submitting = true;

        const { error } = await tryExecute(
            this,
            ApprovalsService.postApprovalsByRunIdStepsByStepIdDecision({
                path: { runId: this.data.runId, stepId: this.data.stepId },
                body: { outcome: outcome, comment: this._comment || undefined },
            }),
        );

        this._submitting = false;

        if (error) {
            // TODO: Show error notification when UMB_NOTIFICATION_CONTEXT is available
            console.error(this.localize.term("uaApproval_decisionError"), error);
            return;
        }

        this.value = { outcome };
        this.modalContext?.submit();
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
                        .disabled=${this._submitting}
                    ></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.localize.term("uaApproval_approve")}
                        @click=${() => this.#onDecision("Approved")}
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
