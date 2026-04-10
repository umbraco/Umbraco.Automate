import { css, html, customElement, state, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { ChannelConfigurationModel, NotifyOnModel, NotificationChannelItemResponseModel } from "../../../../api/types.gen.js";

const NOTIFY_ON_OPTIONS: Array<{ name: string; value: NotifyOnModel }> = [
    { name: "Never", value: "Never" },
    { name: "Failed", value: "Failed" },
    { name: "Suspended", value: "Suspended" },
    { name: "Failed or Suspended", value: "FailedOrSuspended" },
    { name: "Completed", value: "Completed" },
    { name: "Recovered", value: "Recovered" },
];

@customElement("ua-automation-notifications-workspace-view")
export class UaAutomationNotificationsWorkspaceViewElement extends UmbLitElement {
    #workspaceContext?: typeof UA_AUTOMATION_WORKSPACE_CONTEXT.TYPE;
    #catalogueRepo: UaCatalogueRepository;

    @state()
    private _availableChannels: NotificationChannelItemResponseModel[] = [];

    @state()
    private _channels: ChannelConfigurationModel[] = [];

    constructor() {
        super();
        this.#catalogueRepo = new UaCatalogueRepository(this);

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.#workspaceContext = context;
            this.observe(context.data, (model) => {
                if (!model) return;
                this._channels = model.notificationSettings?.channels
                    ? structuredClone(model.notificationSettings.channels)
                    : [];
            });
        });
    }

    override async connectedCallback() {
        super.connectedCallback();
        const { data } = await this.#catalogueRepo.requestNotificationChannels();
        this._availableChannels = data ?? [];
    }

    #addChannel() {
        if (this._availableChannels.length === 0) return;

        // Find first channel not already configured
        const configured = new Set(this._channels.map((c) => c.channelAlias));
        const available = this._availableChannels.find((c) => !configured.has(c.alias));
        if (!available) return;

        this._channels = [
            ...this._channels,
            {
                channelAlias: available.alias,
                settings: {},
                isEnabled: true,
                notifyOn: "Failed",
            },
        ];
        this.#persist();
    }

    #removeChannel(index: number) {
        this._channels = this._channels.filter((_, i) => i !== index);
        this.#persist();
    }

    #onChannelAliasChange(index: number, e: Event) {
        const select = e.target as HTMLSelectElement;
        const updated = [...this._channels];
        updated[index] = { ...updated[index], channelAlias: select.value };
        this._channels = updated;
        this.#persist();
    }

    #onNotifyOnChange(index: number, e: Event) {
        const select = e.target as HTMLSelectElement;
        const updated = [...this._channels];
        updated[index] = { ...updated[index], notifyOn: select.value as NotifyOnModel };
        this._channels = updated;
        this.#persist();
    }

    #onEnabledChange(index: number, e: Event) {
        const toggle = e.target as HTMLInputElement;
        const updated = [...this._channels];
        updated[index] = { ...updated[index], isEnabled: toggle.checked };
        this._channels = updated;
        this.#persist();
    }

    #persist() {
        this.#workspaceContext?.updateProperty("notificationSettings", {
            channels: this._channels,
        });
    }

    #renderChannel(channel: ChannelConfigurationModel, index: number) {
        return html`
            <uui-box>
                <div class="channel-row">
                    <div class="channel-field">
                        <label>${this.localize.term("uaNotifications_channel")}</label>
                        <uui-select
                            .options=${this._availableChannels.map((c) => ({
                                name: c.name,
                                value: c.alias,
                                selected: c.alias === channel.channelAlias,
                            }))}
                            @change=${(e: Event) => this.#onChannelAliasChange(index, e)}
                        ></uui-select>
                    </div>
                    <div class="channel-field">
                        <label>${this.localize.term("uaNotifications_notifyOn")}</label>
                        <uui-select
                            .options=${NOTIFY_ON_OPTIONS.map((o) => ({
                                ...o,
                                selected: o.value === channel.notifyOn,
                            }))}
                            @change=${(e: Event) => this.#onNotifyOnChange(index, e)}
                        ></uui-select>
                    </div>
                    <div class="channel-field">
                        <label>${this.localize.term("uaLabels_enabled")}</label>
                        <uui-toggle
                            ?checked=${channel.isEnabled}
                            @change=${(e: Event) => this.#onEnabledChange(index, e)}
                        ></uui-toggle>
                    </div>
                    <uui-button
                        look="secondary"
                        compact
                        label=${this.localize.term("uaGeneral_delete")}
                        @click=${() => this.#removeChannel(index)}
                    >
                        <uui-icon name="icon-trash"></uui-icon>
                    </uui-button>
                </div>
            </uui-box>
        `;
    }

    override render() {
        return html`
            <div class="container">
                <uui-box headline=${this.localize.term("uaNotifications_headline")}>
                    <p class="description">${this.localize.term("uaNotifications_description")}</p>

                    ${this._channels.length > 0
                        ? html`
                              <div class="channels">
                                  ${repeat(
                                      this._channels,
                                      (_, i) => i,
                                      (channel, index) => this.#renderChannel(channel, index),
                                  )}
                              </div>
                          `
                        : html`<p class="empty">${this.localize.term("uaNotifications_noChannels")}</p>`}

                    <uui-button
                        look="placeholder"
                        label=${this.localize.term("uaNotifications_addChannel")}
                        @click=${this.#addChannel}
                        ?disabled=${this._channels.length >= this._availableChannels.length}
                    >
                        ${this.localize.term("uaNotifications_addChannel")}
                    </uui-button>
                </uui-box>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            .container {
                padding: var(--uui-size-layout-1);
            }

            .description {
                color: var(--uui-color-text-alt);
                margin: 0 0 var(--uui-size-space-5);
            }

            .channels {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-4);
                margin-bottom: var(--uui-size-space-5);
            }

            .channel-row {
                display: flex;
                align-items: flex-end;
                gap: var(--uui-size-space-4);
            }

            .channel-field {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-2);
                flex: 1;
            }

            .channel-field label {
                font-size: var(--uui-type-small-size);
                font-weight: 600;
                color: var(--uui-color-text-alt);
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
                padding: var(--uui-size-space-5);
            }
        `,
    ];
}

export default UaAutomationNotificationsWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-notifications-workspace-view": UaAutomationNotificationsWorkspaceViewElement;
    }
}
