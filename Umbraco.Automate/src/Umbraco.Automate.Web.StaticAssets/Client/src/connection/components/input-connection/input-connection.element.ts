import { css, customElement, html, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UMB_ITEM_PICKER_MODAL, type UmbItemPickerModel } from "@umbraco-cms/backoffice/modal";
import { UaConnectionCollectionRepository } from "../../repository/collection/connection-collection.repository.js";
import type { UaConnectionItemModel } from "../../types.js";
import { UA_CONNECTION_ICON } from "../../constants.js";

@customElement("ua-input-connection")
export class UaInputConnectionElement extends UmbLitElement {
    @property({ type: Array })
    public selection: string[] = [];

    @state()
    private _items: UaConnectionItemModel[] = [];

    #repository: UaConnectionCollectionRepository;

    constructor() {
        super();
        this.#repository = new UaConnectionCollectionRepository(this);
    }

    override async connectedCallback() {
        super.connectedCallback();
        await this.#loadItems();
    }

    async #loadItems() {
        const { data } = await this.#repository.requestCollection({ skip: 0, take: 1000 });
        if (data) {
            this._items = data.items as UaConnectionItemModel[];
        }
    }

    async #openPicker() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const available = this._items.filter((item) => !this.selection.includes(item.unique));

        const modal = modalManager.open(this, UMB_ITEM_PICKER_MODAL, {
            data: {
                headline: "Select Connection",
                items: available.map(
                    (item): UmbItemPickerModel => ({
                        label: item.name,
                        value: item.unique,
                        icon: UA_CONNECTION_ICON,
                        description: item.type,
                    }),
                ),
            },
        });

        try {
            const result = await modal.onSubmit();
            if (result?.value) {
                this.selection = [...this.selection, result.value];
                this.#dispatchChange();
            }
        } catch {
            // Modal dismissed
        }
    }

    #removeItem(unique: string) {
        this.selection = this.selection.filter((id) => id !== unique);
        this.#dispatchChange();
    }

    #dispatchChange() {
        this.dispatchEvent(new CustomEvent("change", { bubbles: true, composed: true }));
    }

    #getItemName(unique: string): string {
        return this._items.find((item) => item.unique === unique)?.name ?? unique;
    }

    override render() {
        return html`
            <uui-ref-list>
                ${this.selection.length > 0
                    ? repeat(
                          this.selection,
                          (id) => id,
                          (id) => this.#renderSelectedItem(id),
                      )
                    : nothing}
            </uui-ref-list>
            <uui-button
                id="btn-add"
                look="placeholder"
                @click=${this.#openPicker}
                label=${this.localize.term("general_choose")}
            ></uui-button>
        `;
    }

    #renderSelectedItem(unique: string) {
        return html`
            <umb-ref-item name=${this.#getItemName(unique)} icon=${UA_CONNECTION_ICON}>
                <uui-action-bar slot="actions">
                    <uui-button
                        label=${this.localize.term("general_remove")}
                        @click=${() => this.#removeItem(unique)}
                    ></uui-button>
                </uui-action-bar>
            </umb-ref-item>
        `;
    }

    static override styles = [
        css`
            #btn-add {
                width: 100%;
            }
        `,
    ];
}

export default UaInputConnectionElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-input-connection": UaInputConnectionElement;
    }
}
