import { html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { UaNodeSettingsModalData, UaNodeSettingsModalValue } from "./types.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import type { BindingSource } from "../../../core/utils/binding-context.utils.js";
import { buildBindingSources } from "../../../core/utils/binding-context.utils.js";
import { UaCatalogueRepository } from "../../../catalogue/repository/catalogue.repository.js";
import "../../../core/components/settings-form/settings-form.element.js";

@customElement("ua-node-settings-modal")
export class UaNodeSettingsModalElement extends UmbModalBaseElement<
    UaNodeSettingsModalData,
    UaNodeSettingsModalValue
> {
    @state()
    private _settings: Record<string, unknown> = {};

    @state()
    private _bindingSources: BindingSource[] = [];

    #catalogueRepo = new UaCatalogueRepository(this);

    override connectedCallback() {
        super.connectedCallback();
        this._settings = { ...this.data?.settings };
        this.#loadBindingSources();
    }

    async #loadBindingSources() {
        const ctx = this.data?.automationContext;
        if (!ctx || !this.data?.stepId) return;

        this._bindingSources = await buildBindingSources(
            this.data.stepId,
            ctx.trigger ?? null,
            ctx.steps,
            ctx.connections,
            this.#catalogueRepo,
        );
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
            <umb-body-layout .headline=${this.data.actionName}>
                <div id="content">
                    <ua-settings-form
                        .fields=${this.data.schema.fields}
                        .values=${this._settings}
                        .bindingSources=${this._bindingSources}
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

export default UaNodeSettingsModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-node-settings-modal": UaNodeSettingsModalElement;
    }
}
