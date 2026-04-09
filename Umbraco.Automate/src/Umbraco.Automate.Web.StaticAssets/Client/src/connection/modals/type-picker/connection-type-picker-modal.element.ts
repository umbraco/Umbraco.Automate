import { css, html, customElement, state, repeat, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UaCatalogueRepository } from "../../../catalogue/repository/catalogue.repository.js";
import type { UaConnectionTypeCatalogueItemModel } from "../../../catalogue/types.js";
import type { UaConnectionTypePickerModalValue } from "./connection-type-picker-modal.token.js";

@customElement("ua-connection-type-picker-modal")
export class UaConnectionTypePickerModalElement extends UmbModalBaseElement<never, UaConnectionTypePickerModalValue> {
    @state()
    private _types: UaConnectionTypeCatalogueItemModel[] = [];

    @state()
    private _loading = true;

    #repository?: UaCatalogueRepository;

    override connectedCallback() {
        super.connectedCallback();
        this.#repository = new UaCatalogueRepository(this);
        this.#loadTypes();
    }

    async #loadTypes() {
        this._loading = true;
        const { data } = await this.#repository!.requestConnectionTypes();
        if (data) {
            this._types = data;
        }
        this._loading = false;
    }

    #onSelect(alias: string) {
        this.value = { typeAlias: alias };
        this.modalContext?.submit();
    }

    #onClose() {
        this.modalContext?.reject();
    }

    override render() {
        return html`
            <umb-body-layout headline=${this.localize.term("uaLabels_connectionType")}>
                <div id="main">
                    ${when(this._loading, () => html`<div id="loader"><uui-loader></uui-loader></div>`)}
                    ${when(
                        !this._loading && this._types.length === 0,
                        () => html`<p class="empty">${this.localize.term("uaCatalogue_noResults")}</p>`,
                    )}
                    ${when(
                        !this._loading,
                        () => html`
                            <uui-box>
                                <uui-ref-list>
                                    ${repeat(
                                        this._types,
                                        (t) => t.alias,
                                        (t) => html`
                                            <uui-ref-node
                                                name=${t.name}
                                                detail=${t.description ?? ""}
                                                @open=${() => this.#onSelect(t.alias)}
                                            >
                                                <umb-icon slot="icon" name=${t.icon || "icon-plugin"}></umb-icon>
                                            </uui-ref-node>
                                        `,
                                    )}
                                </uui-ref-list>
                            </uui-box>
                        `,
                    )}
                </div>

                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onClose}
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
                gap: var(--uui-size-space-4);
            }

            #loader {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-1);
                min-height: 200px;
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
            }
        `,
    ];
}

export default UaConnectionTypePickerModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-type-picker-modal": UaConnectionTypePickerModalElement;
    }
}
