import { css, html, customElement, state, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_ITEM_PICKER_MODAL } from "@umbraco-cms/backoffice/modal";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { ChannelConfigurationModel, NotifyOnModel, NotificationChannelItemResponseModel } from "../../../../api/types.gen.js";
import { UA_NOTIFICATION_CHANNEL_MODAL } from "../../../modals/notification-channel/notification-channel-modal.token.js";

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
        if (!modalManager || this._availableChannels.length === 0) return;

        const picker = modalManager.open(this, UMB_ITEM_PICKER_MODAL, {
            data: {
                headline: this.localize.term("uaNotifications_addChannel"),
                items: this._availableChannels.map((c) => ({
                    label: c.name,
                    value: c.alias,
                    icon: c.icon ?? "icon-message",
                    description: c.description ?? undefined,
                })),
            },
        });

        try {
            const { value: channelAlias } = await picker.onSubmit();
            const catalogueItem = this.#getCatalogueItem(channelAlias);
            if (!catalogueItem) return;

            const newChannel: ChannelConfigurationModel = {
                channelAlias,
                settings: {},
                isEnabled: true,
                notifyOn: "Failed",
            };

            // Open edit modal immediately so the user can configure the new channel.
            const editResult = await this.#openEditModal(newChannel, catalogueItem);
            if (!editResult) return;

            this._channels = [...this._channels, editResult];
            this.#persist();
        } catch {
            // Picker dismissed
        }
    }

    async #editChannel(index: number) {
        const channel = this._channels[index];
        const catalogueItem = this.#getCatalogueItem(channel.channelAlias);
        if (!catalogueItem) return;

        const result = await this.#openEditModal(channel, catalogueItem);
        if (!result) return;

        const updated = [...this._channels];
        updated[index] = result;
        this._channels = updated;
        this.#persist();
    }

    async #openEditModal(
        channel: ChannelConfigurationModel,
        catalogueItem: NotificationChannelItemResponseModel,
    ): Promise<ChannelConfigurationModel | null> {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return null;

        const modal = modalManager.open(this, UA_NOTIFICATION_CHANNEL_MODAL, {
            data: { channel, catalogueItem },
        });

        try {
            const { channel: edited } = await modal.onSubmit();
            return edited;
        } catch {
            return null;
        }
    }

    #removeChannel(index: number, e: Event) {
        e.stopPropagation();
        this._channels = this._channels.filter((_, i) => i !== index);
        this.#persist();
    }

    #persist() {
        this.#workspaceContext?.updateProperty("notificationSettings", {
            channels: this._channels,
        });
    }

    #notifyOnColor(notifyOn: NotifyOnModel): string {
        switch (notifyOn) {
            case "Failed":
            case "FailedOrSuspended":
                return "danger";
            case "Suspended":
                return "warning";
            case "Completed":
            case "Recovered":
                return "positive";
            default:
                return "default";
        }
    }

    #renderChannel(channel: ChannelConfigurationModel, index: number) {
        const item = this.#getCatalogueItem(channel.channelAlias);
        const name = item?.name ?? channel.channelAlias;
        const icon = item?.icon ?? "icon-message";

        return html`
            <uui-ref-node
                name=${name}
                detail=${channel.notifyOn}
                @open=${() => this.#editChannel(index)}
            >
                <umb-icon slot="icon" name=${icon}></umb-icon>
                <div slot="tag">
                    <uui-tag look="secondary" color=${this.#notifyOnColor(channel.notifyOn)}>
                        ${channel.notifyOn}
                    </uui-tag>
                    ${!channel.isEnabled
                        ? html`<uui-tag look="secondary" color="warning">Disabled</uui-tag>`
                        : nothing}
                </div>
                <uui-action-bar slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_edit")}
                        @click=${() => this.#editChannel(index)}
                    >
                        <uui-icon name="icon-edit"></uui-icon>
                    </uui-button>
                    <uui-button
                        label=${this.localize.term("uaGeneral_delete")}
                        @click=${(e: Event) => this.#removeChannel(index, e)}
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
                display: block;
                margin-bottom: var(--uui-size-space-5);
            }

            uui-button[look="placeholder"] {
                width: 100%;
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
