import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import {
    UMB_ELEMENT_FOLDER_PICKER_MODAL,
    UMB_ELEMENT_PICKER_MODAL,
    UmbElementFolderItemRepository,
    UmbElementItemRepository,
} from "@umbraco-cms/backoffice/element";
import type { UmbElementTreeItemModel } from "@umbraco-cms/backoffice/element";
import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";

/**
 * Property editor UI that picks an element or element folder from the Library and stores the key
 * as a string. Used by settings fields that hold an element key. Binding support is not built in:
 * the generic `ua-bindable-editor` wrapper adds the switch to a `${ }` expression around it.
 *
 * The picker modal is opened directly rather than through the CMS's `umb-input-element`: that
 * input opens its modal through a modal route, which needs a router context the settings
 * sidebar does not have, so its Choose button would have no `href` and do nothing.
 *
 * Config: `folderOnly` (boolean, default false). When true the picker lists folders only;
 * otherwise it lists elements only.
 */
@customElement("ua-element-key-picker")
export class UaElementKeyPickerElement
    extends UmbFormControlMixin<string | undefined, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: Boolean, reflect: true })
    readonly = false;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        this._folderOnly = Boolean(config.getValueByAlias("folderOnly"));
    }

    @state()
    private _folderOnly = false;

    @state()
    private _item?: UmbEntityModel;

    @state()
    private _notFound = false;

    #elementItems = new UmbElementItemRepository(this);
    #folderItems = new UmbElementFolderItemRepository(this);
    #loadedKey?: string;

    constructor() {
        super();
        // Enforce mandatory on the host: the editor stores a single key, so empty means no value.
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !this.readonly && !!this.mandatory && !this.value,
        );
    }

    protected override getFormElement() {
        return undefined;
    }

    protected override willUpdate() {
        // The value can be set from outside (a reopened step), so the card follows it.
        if (this.value === this.#loadedKey) return;
        this.#loadedKey = this.value;
        void this.#loadItem(this.value);
    }

    async #loadItem(key: string | undefined) {
        this._item = undefined;
        this._notFound = false;
        if (!key) return;
        const repository = this._folderOnly ? this.#folderItems : this.#elementItems;
        const { data, error } = await repository.requestItems([key]);
        // A newer value may have been picked while this request was in flight.
        if (this.value !== key) return;
        this._item = data?.[0];
        // A deleted or invisible element leaves no item; show an error row so it can still be removed.
        this._notFound = !!error || !this._item;
    }

    async #choose() {
        const modal = this._folderOnly
            ? umbOpenModal(this, UMB_ELEMENT_FOLDER_PICKER_MODAL, { data: { hideTreeRoot: true } })
            : umbOpenModal(this, UMB_ELEMENT_PICKER_MODAL, {
                  data: { hideTreeRoot: true, pickableFilter: (item: UmbElementTreeItemModel) => !item.isFolder },
              });
        // Closing the modal without choosing rejects; the value stays as it was.
        const picked = await modal.then(
            (result) => result.selection[0] ?? undefined,
            () => undefined,
        );
        if (!picked) return;
        this.value = picked;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #remove() {
        this.value = undefined;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        if (this.value) {
            return html`<uui-ref-list>
                <umb-entity-item-ref
                    id=${this.value}
                    .item=${this._item}
                    ?error=${this._notFound}
                    error-message="#general_notFound"
                    ?readonly=${this.readonly}
                    standalone
                >
                    ${this.readonly
                        ? nothing
                        : html`<uui-action-bar slot="actions">
                              <uui-button label=${this.localize.term("general_remove")} @click=${this.#remove}></uui-button>
                          </uui-action-bar>`}
                </umb-entity-item-ref>
            </uui-ref-list>`;
        }
        return html`<uui-button
            id="btn-add"
            look="placeholder"
            label=${this.localize.term("general_choose")}
            ?disabled=${this.readonly}
            @click=${this.#choose}
        ></uui-button>`;
    }

    static override styles = css`
        #btn-add {
            display: block;
        }
    `;
}

export default UaElementKeyPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-element-key-picker": UaElementKeyPickerElement;
    }
}
