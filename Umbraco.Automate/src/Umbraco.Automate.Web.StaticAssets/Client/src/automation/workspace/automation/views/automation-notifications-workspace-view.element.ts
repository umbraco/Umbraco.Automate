import { css, html, customElement, state, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT, UMB_ITEM_PICKER_MODAL } from "@umbraco-cms/backoffice/modal";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { ChannelConfigurationModel, NotifyOnModel, NotificationChannelItemResponseModel } from "../../../../api/types.gen.js";
import "../../../../core/components/settings-form/settings-form.element.js";

const NOTIFY_ON_OPTIONS: Array<{ name: string; value: NotifyOnModel }> = [
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

    @state()
    private _expandedIndex: number | null = null;

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
            const { value } = await picker.onSubmit();
            this._channels = [
                ...this._channels,
                {
                    channelAlias: value,
                    settings: {},
                    isEnabled: true,
                    notifyOn: "Failed",
                },
            ];
            this._expandedIndex = this._channels.length - 1;
            this.#persist();
        } catch {
            // Dismissed
        }
    }

    #removeChannel(index: number) {
        this._channels = this._channels.filter((_, i) => i !== index);
        if (this._expandedIndex === index) this._expandedIndex = null;
        else if (this._expandedIndex !== null && this._expandedIndex > index) this._expandedIndex--;
        this.#persist();
    }

    #toggleExpand(index: number) {
        this._expandedIndex = this._expandedIndex === index ? null : index;
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

    #onSettingsChange(index: number, e: CustomEvent<{ settings: Record<string, unknown> }>) {
        const updated = [...this._channels];
        updated[index] = { ...updated[index], settings: e.detail.settings };
        this._channels = updated;
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
        const isExpanded = this._expandedIndex === index;
        const fields = item?.settingsSchema?.fields ?? [];

        return html`
            <div class="channel">
                <button
                    class="channel-header"
                    type="button"
                    @click=${() => this.#toggleExpand(index)}
                >
                    <uui-icon name=${isExpanded ? "icon-navigation-down" : "icon-navigation-right"}></uui-icon>
                    <umb-icon name=${icon}></umb-icon>
                    <span class="channel-name">${name}</span>
                    <span class="channel-detail">${channel.notifyOn}</span>
                    ${!channel.isEnabled
                        ? html`<uui-tag look="secondary" color="default">Disabled</uui-tag>`
                        : nothing}
                    <uui-button
                        class="channel-delete"
                        look="secondary"
                        compact
                        label=${this.localize.term("uaGeneral_delete")}
                        @click=${(e: Event) => { e.stopPropagation(); this.#removeChannel(index); }}
                    >
                        <uui-icon name="icon-trash"></uui-icon>
                    </uui-button>
                </button>
                ${isExpanded
                    ? html`
                          <div class="channel-body">
                              <div class="channel-fields">
                                  <umb-property-layout label=${this.localize.term("uaNotifications_notifyOn")} orientation="vertical">
                                      <div slot="editor">
                                          <uui-select
                                              .options=${NOTIFY_ON_OPTIONS.map((o) => ({
                                                  ...o,
                                                  selected: o.value === channel.notifyOn,
                                              }))}
                                              @change=${(e: Event) => this.#onNotifyOnChange(index, e)}
                                          ></uui-select>
                                      </div>
                                  </umb-property-layout>
                                  <umb-property-layout label=${this.localize.term("uaLabels_enabled")} orientation="vertical">
                                      <div slot="editor">
                                          <uui-toggle
                                              ?checked=${channel.isEnabled}
                                              @change=${(e: Event) => this.#onEnabledChange(index, e)}
                                              label=${this.localize.term("uaLabels_enabled")}
                                          ></uui-toggle>
                                      </div>
                                  </umb-property-layout>
                              </div>
                              ${fields.length > 0
                                  ? html`
                                        <ua-settings-form
                                            no-box
                                            .fields=${fields}
                                            .values=${channel.settings}
                                            @ua:settings-change=${(e: CustomEvent<{ settings: Record<string, unknown> }>) => this.#onSettingsChange(index, e)}
                                        ></ua-settings-form>
                                    `
                                  : nothing}
                          </div>
                      `
                    : nothing}
            </div>
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
                margin-bottom: var(--uui-size-space-5);
                border: 1px solid var(--uui-color-border);
                border-radius: var(--uui-border-radius);
                overflow: hidden;
            }

            .channel + .channel {
                border-top: 1px solid var(--uui-color-border);
            }

            .channel-header {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                width: 100%;
                padding: var(--uui-size-space-3) var(--uui-size-space-4);
                border: none;
                background: var(--uui-color-surface);
                cursor: pointer;
                font-size: var(--uui-type-default-size);
                color: var(--uui-color-text);
            }

            .channel-header:hover {
                background: var(--uui-color-surface-alt);
            }

            .channel-name {
                font-weight: 600;
            }

            .channel-detail {
                flex: 1;
                text-align: left;
                color: var(--uui-color-text-alt);
                font-size: var(--uui-type-small-size);
            }

            .channel-delete {
                flex-shrink: 0;
            }

            .channel-body {
                padding: var(--uui-size-space-5);
                border-top: 1px solid var(--uui-color-border);
                background: var(--uui-color-surface);
            }

            .channel-fields {
                margin-bottom: var(--uui-size-space-4);
            }

            .channel-fields uui-select {
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
