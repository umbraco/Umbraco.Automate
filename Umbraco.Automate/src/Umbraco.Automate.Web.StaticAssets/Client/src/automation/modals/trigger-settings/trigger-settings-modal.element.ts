import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaTriggerSettingsModalData, UaTriggerSettingsModalValue } from "./types.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import "../../../core/components/settings-form/settings-form.element.js";

@customElement("ua-trigger-settings-modal")
export class UaTriggerSettingsModalElement extends UmbModalBaseElement<
    UaTriggerSettingsModalData,
    UaTriggerSettingsModalValue
> {
    @state()
    private _settings: Record<string, unknown> = {};

    override connectedCallback() {
        super.connectedCallback();
        this._settings = { ...this.data?.settings };
    }

    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        this._settings = event.detail.settings;
    }

    #onSubmit() {
        this.value = { settings: this._settings };
        this.modalContext?.submit();
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this.data) return html``;

        return html`
            <umb-body-layout .headline=${this.data.triggerAlias}>
                <div id="content">
                    <ua-settings-form
                        .fields=${this.data.schema.fields}
                        .values=${this._settings}
                        @ua:settings-change=${this.#onSettingsChange}
                    ></ua-settings-form>
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
}

export default UaTriggerSettingsModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-trigger-settings-modal": UaTriggerSettingsModalElement;
    }
}
