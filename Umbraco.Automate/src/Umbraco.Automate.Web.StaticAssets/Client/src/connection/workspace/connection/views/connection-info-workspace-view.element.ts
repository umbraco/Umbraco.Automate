import { css, html, customElement, state, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UUIInputElement, UUIInputEvent } from "@umbraco-cms/backoffice/external/uui";
import type { UaConnectionDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_CONNECTION_WORKSPACE_CONTEXT } from "../connection-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { UaConnectionTypeCatalogueItemModel } from "../../../../catalogue/types.js";

@customElement("ua-connection-info-workspace-view")
export class UaConnectionInfoWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaConnectionDetailModel;

    @state()
    private _connectionTypes: UaConnectionTypeCatalogueItemModel[] = [];

    @state()
    private _settingsEntries: Array<{ key: string; value: string }> = [];

    #workspaceContext?: typeof UA_CONNECTION_WORKSPACE_CONTEXT.TYPE;
    #catalogueRepository: UaCatalogueRepository;

    constructor() {
        super();
        this.#catalogueRepository = new UaCatalogueRepository(this);

        this.consumeContext(UA_CONNECTION_WORKSPACE_CONTEXT, (context) => {
            if (context) {
                this.#workspaceContext = context;
                this.observe(context.data, (model) => {
                    this._model = model;
                    if (model) {
                        this._settingsEntries = Object.entries(model.settings).map(([key, value]) => ({
                            key,
                            value: String(value ?? ""),
                        }));
                    }
                });
            }
        });
    }

    override async connectedCallback() {
        super.connectedCallback();
        const { data } = await this.#catalogueRepository.requestConnectionTypes();
        if (data) {
            this._connectionTypes = data;
        }
    }

    #onTypeChange(event: Event) {
        event.stopPropagation();
        const target = event.target as HTMLSelectElement;
        const alias = target.value;
        this.#workspaceContext?.updateProperty("type", alias);

        // Reset settings when type changes — different types have different schemas
        this._settingsEntries = [];
        this.#workspaceContext?.updateProperty("settings", {});
    }

    #onSettingKeyChange(index: number, event: UUIInputEvent) {
        event.stopPropagation();
        const target = event.composedPath()[0] as UUIInputElement;
        this._settingsEntries[index].key = target.value.toString();
        this.#syncSettings();
    }

    #onSettingValueChange(index: number, event: UUIInputEvent) {
        event.stopPropagation();
        const target = event.composedPath()[0] as UUIInputElement;
        this._settingsEntries[index].value = target.value.toString();
        this.#syncSettings();
    }

    #addSetting() {
        this._settingsEntries = [...this._settingsEntries, { key: "", value: "" }];
    }

    #removeSetting(index: number) {
        this._settingsEntries = this._settingsEntries.filter((_, i) => i !== index);
        this.#syncSettings();
    }

    #syncSettings() {
        const settings: Record<string, unknown> = {};
        for (const entry of this._settingsEntries) {
            if (entry.key) {
                settings[entry.key] = entry.value;
            }
        }
        this.#workspaceContext?.updateProperty("settings", settings);
    }

    #renderTypeField() {
        if (!this._model) return nothing;

        const options = [
            { name: "Select connection type\u2026", value: "", selected: !this._model.type },
            ...this._connectionTypes.map((ct) => ({
                name: ct.name,
                value: ct.alias,
                selected: ct.alias === this._model!.type,
            })),
        ];

        return html`
            <uui-select
                .options=${options}
                @change=${this.#onTypeChange}
                label="Type"
            ></uui-select>
        `;
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="container">
                <uui-box headline="Connection Type">
                    <umb-property-layout label="Type" orientation="vertical">
                        <div slot="editor">
                            ${this.#renderTypeField()}
                        </div>
                    </umb-property-layout>
                </uui-box>

                <uui-box headline="Settings">
                    <umb-property-layout orientation="vertical">
                        <div slot="editor">
                            ${repeat(
                                this._settingsEntries,
                                (_entry, index) => index,
                                (entry, index) => html`
                                    <div class="setting-row">
                                        <uui-input
                                            .value=${entry.key}
                                            @input=${(e: UUIInputEvent) => this.#onSettingKeyChange(index, e)}
                                            label="Key"
                                            placeholder="Key"
                                        ></uui-input>
                                        <uui-input
                                            .value=${entry.value}
                                            @input=${(e: UUIInputEvent) => this.#onSettingValueChange(index, e)}
                                            label="Value"
                                            placeholder="Value"
                                        ></uui-input>
                                        <uui-button
                                            look="secondary"
                                            color="danger"
                                            label="Remove"
                                            compact
                                            @click=${() => this.#removeSetting(index)}
                                        >
                                            <uui-icon name="icon-trash"></uui-icon>
                                        </uui-button>
                                    </div>
                                `,
                            )}
                            <uui-button look="placeholder" label="Add setting" @click=${this.#addSetting}>
                                Add setting
                            </uui-button>
                        </div>
                    </umb-property-layout>
                </uui-box>
            </div>

            <div class="container">
                <uui-box headline="History">
                    ${this._model.dateCreated
                        ? html`
                              <umb-property-layout label="Created" orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateCreated)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                    ${this._model.dateModified
                        ? html`
                              <umb-property-layout label="Modified" orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateModified)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                </uui-box>

                <uui-box headline=${this.localize.term("general_general")}>
                    <umb-property-layout label="Id" orientation="vertical">
                        <div slot="editor">
                            ${this._model.unique === UA_EMPTY_GUID
                                ? html`<uui-tag color="default" look="placeholder">Unsaved</uui-tag>`
                                : this._model.unique}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Alias" orientation="vertical">
                        <div slot="editor">${this._model.alias || "-"}</div>
                    </umb-property-layout>
                </uui-box>
            </div>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            :host {
                display: grid;
                gap: var(--uui-size-layout-1);
                padding: var(--uui-size-layout-1);
                grid-template-columns: 1fr 350px;
            }

            .container {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            .setting-row {
                display: flex;
                gap: var(--uui-size-space-3);
                align-items: center;
                margin-bottom: var(--uui-size-space-3);
            }

            .setting-row uui-input {
                flex: 1;
            }

            uui-select {
                width: 100%;
            }

             uui-box {
                --uui-box-default-padding: 0 var(--uui-size-space-5);
            }

            umb-property-layout[orientation="vertical"]:not(:last-child) {
                padding-bottom: 0;
            }

            uui-loader {
                display: block;
                margin: auto;
                position: absolute;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
            }
        `,
    ];
}

export default UaConnectionInfoWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-info-workspace-view": UaConnectionInfoWorkspaceViewElement;
    }
}
