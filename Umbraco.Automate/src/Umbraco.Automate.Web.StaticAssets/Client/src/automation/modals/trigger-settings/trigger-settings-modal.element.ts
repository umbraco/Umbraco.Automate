import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbValidationContext } from "@umbraco-cms/backoffice/validation";
import type { UaTriggerSettingsModalData, UaTriggerSettingsModalValue } from "./types.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import { UA_WEBHOOK_TRIGGER_ALIAS } from "../../triggers/constants.js";
import "../../../core/components/settings-form/settings-form.element.js";
import "./webhook-trigger-panel.element.js";

/** Keys of the webhook trigger's own settings fields, shown inside the webhook panel's box
 * (alongside the URL) rather than the main settings form. */
const UA_WEBHOOK_PANEL_FIELD_KEYS = ["testRequestBody", "testRequestHeaders"];

@customElement("ua-trigger-settings-modal")
export class UaTriggerSettingsModalElement extends UmbModalBaseElement<
    UaTriggerSettingsModalData,
    UaTriggerSettingsModalValue
> {
    @state()
    private _settings: Record<string, unknown> = {};

    // Provides UMB_VALIDATION_CONTEXT to the descendant settings form and its umb-property
    // fields (the context API crosses shadow-DOM boundaries), so their mandatory validators
    // register here and #onSubmit can gate submission on validate().
    #validationContext = new UmbValidationContext(this);

    override connectedCallback() {
        super.connectedCallback();
        this._settings = { ...this.data?.settings };
    }

    // Two independent forms contribute to `_settings` (the main settings form, and the webhook
    // panel's own nested form for its test-request fields) — merge each one's slice in rather
    // than replacing, so saving one doesn't drop the other's values.
    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        this._settings = { ...this._settings, ...event.detail.settings };
    }

    async #onSubmit() {
        try {
            await this.#validationContext.validate();
        } catch {
            // Validation failed (e.g. a required field is empty) — keep the modal open
            // so the field-level errors stay visible.
            return;
        }
        this.value = { settings: this._settings };
        this.modalContext?.submit();
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    /**
     * Webhook triggers get the endpoint URL and their own test-request fields grouped together
     * in one box, so the URL is to hand right alongside the data used to exercise it.
     */
    #renderWebhookPanel() {
        if (this.data?.triggerAlias !== UA_WEBHOOK_TRIGGER_ALIAS) return nothing;

        const testFields = this.data.schema.fields.filter((f) => UA_WEBHOOK_PANEL_FIELD_KEYS.includes(f.key));

        return html`
            <ua-webhook-trigger-panel
                automation-id=${this.data.automationId}
                .testFields=${testFields}
                .values=${this._settings}
                @ua:settings-change=${this.#onSettingsChange}
            ></ua-webhook-trigger-panel>
        `;
    }

    override render() {
        if (!this.data) return html``;

        const mainFields = this.data.schema.fields.filter((f) => !UA_WEBHOOK_PANEL_FIELD_KEYS.includes(f.key));

        return html`
            <umb-body-layout .headline=${this.data.triggerName}>
                <div id="content">
                    <ua-settings-form
                        label-on-top
                        .fields=${mainFields}
                        .values=${this._settings}
                        @ua:settings-change=${this.#onSettingsChange}
                    ></ua-settings-form>
                    ${this.#renderWebhookPanel()}
                </div>
                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onCancel}
                    ></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.localize.term("uaGeneral_save")}
                        @click=${this.#onSubmit}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    static styles = [
        css`
            #content {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }
        `,
    ];
}

export default UaTriggerSettingsModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-trigger-settings-modal": UaTriggerSettingsModalElement;
    }
}
