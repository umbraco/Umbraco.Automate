import { css, html, customElement, property, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";

import "../../../core/components/webhook-url-field/webhook-url-field.element.js";
import "../../../core/components/settings-form/settings-form.element.js";

/**
 * Webhook-specific extras for the trigger settings sidebar, grouped in one box: the endpoint
 * URL (so it can be copied while setting up the signing secret) first, then the trigger's own
 * test-request fields used to exercise the automation on demand.
 */
@customElement("ua-webhook-trigger-panel")
export class UaWebhookTriggerPanelElement extends UmbLitElement {
    /** Unique of the automation this webhook belongs to. */
    @property({ type: String, attribute: "automation-id" })
    automationId = "";

    /** The webhook trigger's own settings fields (test request body/headers) to render below the URL. */
    @property({ type: Array })
    testFields: EditableModelFieldDescriptorModel[] = [];

    /** The trigger's current settings values, keyed by field alias. */
    @property({ type: Object })
    values: Record<string, unknown> = {};

    get #isSaved(): boolean {
        return !!this.automationId && this.automationId !== UA_EMPTY_GUID;
    }

    override render() {
        return html`
            <uui-box headline=${this.localize.term("uaWebhook_headline")}>
                ${this.#isSaved ? this.#renderUrl() : this.#renderUnsavedHint()}
                ${this.#isSaved ? this.#renderTestFields() : nothing}
            </uui-box>
        `;
    }

    #renderUnsavedHint() {
        return html`<p class="hint">${this.localize.term("uaWebhook_unsavedHint")}</p>`;
    }

    #renderUrl() {
        return html`
            <umb-property-layout label=${this.localize.term("uaLabels_webhookUrl")} orientation="vertical">
                <ua-webhook-url-field slot="editor" automation-id=${this.automationId}></ua-webhook-url-field>
            </umb-property-layout>
        `;
    }

    #renderTestFields() {
        if (!this.testFields.length) return nothing;

        // Bubbles a `ua:settings-change` event the modal listens for directly on this element,
        // so it crosses this panel's shadow boundary without the panel itself relaying it.
        return html`
            <ua-settings-form no-box label-on-top .fields=${this.testFields} .values=${this.values}></ua-settings-form>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            /* Match ua-settings-form so the panel's rows sit on the same rhythm as the
               settings fields directly above it. */
            umb-property-layout {
                --uui-size-layout-1: var(--uui-size-space-2);
            }

            .hint {
                margin: 0;
                color: var(--uui-color-text-alt);
            }
        `,
    ];
}

export default UaWebhookTriggerPanelElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-trigger-panel": UaWebhookTriggerPanelElement;
    }
}
