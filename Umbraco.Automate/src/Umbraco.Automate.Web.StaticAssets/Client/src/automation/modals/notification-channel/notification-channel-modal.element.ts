import { html, customElement, state, nothing, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { ChannelConfigurationModel, NotifyOnModel } from "../../../api/types.gen.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import "../../../core/components/settings-form/settings-form.element.js";
import type { UaNotificationChannelModalData, UaNotificationChannelModalValue } from "./types.js";
import { NOTIFY_ON_FLAGS, parseNotifyOn, serializeNotifyOn, type NotifyOnFlag } from "./notify-on.helpers.js";

@customElement("ua-notification-channel-modal")
export class UaNotificationChannelModalElement extends UmbModalBaseElement<
    UaNotificationChannelModalData,
    UaNotificationChannelModalValue
> {
    @state()
    private _channel?: ChannelConfigurationModel;

    @state()
    private _notifyOnFlags = new Set<NotifyOnFlag>();

    override connectedCallback() {
        super.connectedCallback();
        if (this.data?.channel) {
            this._channel = structuredClone(this.data.channel);
            this._notifyOnFlags = parseNotifyOn(this._channel.notifyOn);
        }
    }

    #onNotifyOnFlagToggle(flag: NotifyOnFlag, e: Event) {
        const toggle = e.target as HTMLInputElement;
        if (!this._channel) return;

        const next = new Set(this._notifyOnFlags);
        if (toggle.checked) {
            next.add(flag);
        } else {
            next.delete(flag);
        }

        this._notifyOnFlags = next;
        // The wire format may be a comma-separated combination of NotifyOnFlag values,
        // which the strict NotifyOnModel union can't express — the backend's [Flags] enum
        // accepts it at runtime.
        this._channel = { ...this._channel, notifyOn: serializeNotifyOn(next) as NotifyOnModel };
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
                        <umb-property-layout
                            label=${this.localize.term("uaLabels_enabled")}
                            orientation="vertical"
                        >
                            <div slot="editor">
                                <uui-toggle
                                    ?checked=${this._channel.isEnabled}
                                    @change=${this.#onEnabledChange}
                                    label=${this.localize.term("uaLabels_enabled")}
                                ></uui-toggle>
                            </div>
                        </umb-property-layout>
                    </uui-box>

                    <uui-box .headline=${this.localize.term("uaNotifications_notifyOn")}>
                        <div class="notify-list">
                            ${NOTIFY_ON_FLAGS.map((flag) => {
                                const key = "uaNotifications_notifyOn" + flag;
                                return html`
                                    <div class="notify-row">
                                        <uui-toggle
                                            ?checked=${this._notifyOnFlags.has(flag)}
                                            @change=${(e: Event) => this.#onNotifyOnFlagToggle(flag, e)}
                                            label=${this.localize.term(key)}
                                        ></uui-toggle>
                                        <div class="notify-desc">
                                            ${this.localize.term(key + "Desc")}
                                        </div>
                                    </div>
                                `;
                            })}
                        </div>
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

    static override styles = [
        css`
            #content {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            umb-property-layout[orientation="vertical"] {
                padding-bottom: 0;
            }

            umb-property-layout:first-of-type {
                padding-top: 0;
            }

            .notify-list {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-5);
            }

            .notify-row {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-1);
            }

            .notify-desc {
                color: var(--uui-color-text-alt);
                font-size: var(--uui-type-small-size);
                line-height: 1.4;
            }
        `,
    ];
}

export default UaNotificationChannelModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-notification-channel-modal": UaNotificationChannelModalElement;
    }
}
