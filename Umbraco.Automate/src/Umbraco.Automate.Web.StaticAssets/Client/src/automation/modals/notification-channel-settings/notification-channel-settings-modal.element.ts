import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UaNotificationChannelSettingsModalData,
    UaNotificationChannelSettingsModalValue,
} from "./notification-channel-settings-modal.token.js";
import type { ChannelConfigurationModel, NotifyOnModel } from "../../../api/types.gen.js";
import "../../../core/components/settings-form/settings-form.element.js";

const NOTIFY_ON_OPTIONS: Array<{ name: string; value: NotifyOnModel; selected?: boolean }> = [
    { name: "Failed", value: "Failed" },
    { name: "Suspended", value: "Suspended" },
    { name: "Failed or Suspended", value: "FailedOrSuspended" },
    { name: "Completed", value: "Completed" },
    { name: "Recovered", value: "Recovered" },
];

@customElement("ua-notification-channel-settings-modal")
export class UaNotificationChannelSettingsModalElement extends UmbModalBaseElement<
    UaNotificationChannelSettingsModalData,
    UaNotificationChannelSettingsModalValue
> {
    @state()
    private _channel!: ChannelConfigurationModel;

    override connectedCallback() {
        super.connectedCallback();
        this._channel = structuredClone(this.data!.channel);
    }

    #onNotifyOnChange(e: Event) {
        const select = e.target as HTMLSelectElement;
        this._channel = { ...this._channel, notifyOn: select.value as NotifyOnModel };
    }

    #onEnabledChange(e: Event) {
        const toggle = e.target as HTMLInputElement;
        this._channel = { ...this._channel, isEnabled: toggle.checked };
    }

    #onSettingsChange(e: CustomEvent<{ settings: Record<string, unknown> }>) {
        this._channel = { ...this._channel, settings: e.detail.settings };
    }

    #onSubmit() {
        this.value = { channel: this._channel };
        this.modalContext?.submit();
    }

    #onClose() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this.data) return nothing;

        const { catalogueItem } = this.data;
        const fields = catalogueItem.settingsSchema?.fields ?? [];

        return html`
            <umb-body-layout headline=${catalogueItem.name}>
                <div id="main">
                    <uui-box>
                        <umb-property-layout label=${this.localize.term("uaNotifications_notifyOn")} orientation="vertical">
                            <div slot="editor">
                                <uui-select
                                    .options=${NOTIFY_ON_OPTIONS.map((o) => ({
                                        ...o,
                                        selected: o.value === this._channel.notifyOn,
                                    }))}
                                    @change=${this.#onNotifyOnChange}
                                ></uui-select>
                            </div>
                        </umb-property-layout>
                        <umb-property-layout label=${this.localize.term("uaLabels_enabled")} orientation="vertical">
                            <div slot="editor">
                                <uui-toggle
                                    ?checked=${this._channel.isEnabled}
                                    @change=${this.#onEnabledChange}
                                    label=${this.localize.term("uaLabels_enabled")}
                                ></uui-toggle>
                            </div>
                        </umb-property-layout>
                    </uui-box>

                    ${fields.length > 0
                        ? html`
                              <uui-box headline=${this.localize.term("uaConnection_settings")}>
                                  <ua-settings-form
                                      .fields=${fields}
                                      .values=${this._channel.settings}
                                      @ua:settings-change=${this.#onSettingsChange}
                                  ></ua-settings-form>
                              </uui-box>
                          `
                        : nothing}
                </div>

                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onClose}
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

    static override styles = [
        UmbTextStyles,
        css`
            #main {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            uui-select {
                width: 100%;
            }
        `,
    ];
}

export default UaNotificationChannelSettingsModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-notification-channel-settings-modal": UaNotificationChannelSettingsModalElement;
    }
}
