import { html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { ChannelConfigurationModel, NotifyOnModel } from "../../../api/types.gen.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import "../../../core/components/settings-form/settings-form.element.js";
import type { UaNotificationChannelModalData, UaNotificationChannelModalValue } from "./types.js";

const NOTIFY_ON_OPTIONS: Array<{ name: string; value: NotifyOnModel }> = [
    { name: "Failed", value: "Failed" },
    { name: "Suspended", value: "Suspended" },
    { name: "Failed or Suspended", value: "FailedOrSuspended" },
    { name: "Completed", value: "Completed" },
    { name: "Recovered", value: "Recovered" },
];

@customElement("ua-notification-channel-modal")
export class UaNotificationChannelModalElement extends UmbModalBaseElement<
    UaNotificationChannelModalData,
    UaNotificationChannelModalValue
> {
    @state()
    private _channel?: ChannelConfigurationModel;

    override connectedCallback() {
        super.connectedCallback();
        if (this.data?.channel) {
            this._channel = structuredClone(this.data.channel);
        }
    }

    #onNotifyOnChange(e: Event) {
        const select = e.target as HTMLSelectElement;
        if (!this._channel) return;
        this._channel = { ...this._channel, notifyOn: select.value as NotifyOnModel };
    }

    #onEnabledChange(e: Event) {
        const toggle = e.target as HTMLInputElement;
        if (!this._channel) return;
        this._channel = { ...this._channel, isEnabled: toggle.checked };
    }

    #onSettingsChange(e: CustomEvent<SettingsChangeDetail>) {
        if (!this._channel) return;
        this._channel = { ...this._channel, settings: e.detail.settings };
    }

    #onSubmit() {
        if (!this._channel) return;
        this.value = { channel: this._channel };
        this.modalContext?.submit();
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this.data || !this._channel) return html``;

        const { catalogueItem } = this.data;
        const fields = catalogueItem.settingsSchema?.fields ?? [];

        return html`
            <umb-body-layout .headline=${catalogueItem.name}>
                <div id="content">
                    <uui-box>
                        <umb-property-layout label=${this.localize.term("uaNotifications_notifyOn")}>
                            <div slot="editor">
                                <uui-select
                                    .options=${NOTIFY_ON_OPTIONS.map((o) => ({
                                        ...o,
                                        selected: o.value === this._channel!.notifyOn,
                                    }))}
                                    @change=${this.#onNotifyOnChange}
                                ></uui-select>
                            </div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_enabled")}>
                            <div slot="editor">
                                <uui-toggle
                                    ?checked=${this._channel.isEnabled}
                                    @change=${this.#onEnabledChange}
                                    label=${this.localize.term("uaLabels_enabled")}
                                ></uui-toggle>
                            </div>
                        </umb-property-layout>

                        ${fields.length > 0
                            ? html`
                                  <ua-settings-form
                                      label-on-top
                                      no-box
                                      .fields=${fields}
                                      .values=${this._channel.settings}
                                      @ua:settings-change=${this.#onSettingsChange}
                                  ></ua-settings-form>
                              `
                            : nothing}
                    </uui-box>
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

export default UaNotificationChannelModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-notification-channel-modal": UaNotificationChannelModalElement;
    }
}
