import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaConnectionDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_CONNECTION_WORKSPACE_CONTEXT } from "../connection-workspace.context-token.js";
import { UaCatalogueRepository } from "../../../../catalogue/repository/catalogue.repository.js";
import type { UaConnectionTypeCatalogueItemModel } from "../../../../catalogue/types.js";
import "../../../../core/components/settings-form/settings-form.element.js";
import type { SettingsChangeDetail } from "../../../../core/components/settings-form/settings-form.element.js";

@customElement("ua-connection-info-workspace-view")
export class UaConnectionInfoWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaConnectionDetailModel;

    @state()
    private _connectionTypes: UaConnectionTypeCatalogueItemModel[] = [];

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
        this.#workspaceContext?.updateProperty("settings", {});
    }

    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        event.stopPropagation();
        this.#workspaceContext?.updateProperty("settings", event.detail.settings);
    }

    #getSelectedConnectionType(): UaConnectionTypeCatalogueItemModel | undefined {
        if (!this._model?.type) return undefined;
        return this._connectionTypes.find((ct) => ct.alias === this._model!.type);
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

                ${this.#getSelectedConnectionType()?.settingsSchema?.fields?.length
                    ? html`<uui-box class="settings" headline="Settings">
                          <ua-settings-form
                              no-box
                              .fields=${this.#getSelectedConnectionType()!.settingsSchema!.fields}
                              .values=${this._model.settings}
                              @ua:settings-change=${this.#onSettingsChange}
                          ></ua-settings-form>
                      </uui-box>`
                    : nothing}
            </div>

            <div class="container">
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

            uui-select {
                width: 100%;
            }

            uui-box:not(.settings) {
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
