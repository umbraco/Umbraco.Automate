import { css, html, customElement, state, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_ITEM_PICKER_MODAL } from "@umbraco-cms/backoffice/modal";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import { UA_NOTIFICATION_CHANNEL_SETTINGS_MODAL } from "../../../modals/notification-channel-settings/notification-channel-settings-modal.token.js";
import type { ChannelConfigurationModel, NotificationChannelItemResponseModel } from "../../../../api/types.gen.js";

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

    #getCatalogueItem(alias: string): NotificationChannelItemResponseModel | undefined {
        return this._availableChannels.find((c) => c.alias === alias);
    }

    async #addChannel() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const configured = new Set(this._channels.map((c) => c.channelAlias));
        const available = this._availableChannels.filter((c) => !configured.has(c.alias));
        if (available.length === 0) return;

        const picker = modalManager.open(this, UMB_ITEM_PICKER_MODAL, {
            data: {
                headline: this.localize.term("uaNotifications_addChannel"),
                items: available.map((c) => ({
                    label: c.name,
                    value: c.alias,
                    icon: c.icon ?? "icon-message",
                    description: c.description ?? undefined,
                })),
            },
        });

        try {
            const { value } = await picker.onSubmit();
            const channelAlias = value;
            const catalogueItem = this.#getCatalogueItem(channelAlias);
            if (!catalogueItem) return;

            const newChannel: ChannelConfigurationModel = {
                channelAlias,
                settings: {},
                isEnabled: true,
                notifyOn: "Failed",
            };

            // Open settings modal immediately for the new channel
            const settingsModal = modalManager.open(this, UA_NOTIFICATION_CHANNEL_SETTINGS_MODAL, {
                data: {
                    channel: newChannel,
                    catalogueItem,
                },
            });

            const { channel: configured } = await settingsModal.onSubmit();
            this._channels = [...this._channels, configured];
            this.#persist();
        } catch {
            // Picker or settings modal dismissed
        }
    }

    async #editChannel(index: number) {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const channel = this._channels[index];
        const catalogueItem = this.#getCatalogueItem(channel.channelAlias);
        if (!catalogueItem) return;

        const modal = modalManager.open(this, UA_NOTIFICATION_CHANNEL_SETTINGS_MODAL, {
            data: {
                channel: structuredClone(channel),
                catalogueItem,
            },
        });

        try {
            const { channel: updated } = await modal.onSubmit();
            const updatedChannels = [...this._channels];
            updatedChannels[index] = updated;
            this._channels = updatedChannels;
            this.#persist();
        } catch {
            // Modal dismissed
        }
    }

    #removeChannel(index: number) {
        this._channels = this._channels.filter((_, i) => i !== index);
        this.#persist();
    }

    #persist() {
        this.#workspaceContext?.updateProperty("notificationSettings", {
            channels: this._channels,
        });
    }

    #renderChannel(channel: ChannelConfigurationModel, index: number) {
        const item = this.#getCatalogueItem(channel.channelAlias);
        const name = item?.name ?? channel.channelAlias;
        const icon = item?.icon ?? "icon-message";
        const detail = channel.isEnabled
            ? `Notify on: ${channel.notifyOn}`
            : "Disabled";

        return html`
            <uui-ref-node
                name=${name}
                detail=${detail}
                @open=${() => this.#editChannel(index)}
            >
                <umb-icon slot="icon" name=${icon}></umb-icon>
                ${!channel.isEnabled
                    ? html`<uui-tag slot="tag" look="secondary" color="default">Disabled</uui-tag>`
                    : nothing}
                <uui-action-bar slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_delete")}
                        @click=${() => this.#removeChannel(index)}
                    >
                        <uui-icon name="icon-trash"></uui-icon>
                    </uui-button>
                </uui-action-bar>
            </uui-ref-node>
        `;
    }

    override render() {
        return html`
            <div class="container">
                <uui-box headline=${this.localize.term("uaNotifications_headline")}>
                    <p class="description">${this.localize.term("uaNotifications_description")}</p>

                    ${this._channels.length > 0
                        ? html`
                              <uui-ref-list>
                                  ${repeat(
                                      this._channels,
                                      (_, i) => i,
                                      (channel, index) => this.#renderChannel(channel, index),
                                  )}
                              </uui-ref-list>
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

            uui-ref-list {
                margin-bottom: var(--uui-size-space-5);
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
