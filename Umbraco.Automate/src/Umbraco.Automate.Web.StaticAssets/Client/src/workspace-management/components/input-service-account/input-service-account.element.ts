import { css, customElement, html, ifDefined, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { UmbUserKind, UmbUserPickerInputContext } from "@umbraco-cms/backoffice/user";
import type { UmbUserDetailModel, UmbUserItemModel } from "@umbraco-cms/backoffice/user";

/**
 * Picks the single API user a workspace runs its automations as.
 *
 * Mirrors `<umb-user-input>`, which keeps its picker context private, so the user picker can be
 * limited to API users. The server rejects any other kind of user, so offering them here would
 * only lead to a failed save.
 */
@customElement("ua-input-service-account")
export class UaInputServiceAccountElement extends UmbFormControlMixin<string | undefined, typeof UmbLitElement>(
    UmbLitElement,
    undefined,
) {
    @property({ type: Boolean })
    required?: boolean;

    @property({ type: Array })
    set selection(uniques: Array<string>) {
        this.#pickerContext.setSelection(uniques);
    }
    get selection(): Array<string> {
        return this.#pickerContext.getSelection();
    }

    @property()
    override set value(unique: string | undefined) {
        this.selection = unique ? [unique] : [];
    }
    override get value(): string | undefined {
        return this.selection.join(",");
    }

    @state()
    private _items?: Array<UmbUserItemModel>;

    @state()
    private _statuses?: Array<{ unique: string; state: { type: string; error?: string } }>;

    @state()
    private _modalRoute?: string;

    #pickerContext = new UmbUserPickerInputContext(this);

    constructor() {
        super();

        // The user picker's collection view ignores `filter` and only honours `pickableFilter`, which
        // leaves other users visible but not selectable. Set both so they're hidden wherever the
        // picker supports it.
        const isApiUser = (user: UmbUserDetailModel) => user.kind === UmbUserKind.API;
        this.#pickerContext.max = 1;
        this.#pickerContext.setModalData({ filter: isApiUser, pickableFilter: isApiUser });

        this.addValidator(
            "valueMissing",
            () => UMB_VALIDATION_EMPTY_LOCALIZATION_KEY,
            () => !!this.required && !this.value,
        );

        this.observe(this.#pickerContext.selection, (selection) => (this.value = selection.join(",")), null);
        this.observe(this.#pickerContext.selectedItems, (items) => (this._items = items), null);
        this.observe(this.#pickerContext.statuses, (statuses) => (this._statuses = statuses), null);
        this.observe(this.#pickerContext.modalRoute, (modalRoute) => (this._modalRoute = modalRoute), null);
    }

    protected override getFormElement() {
        return undefined;
    }

    override render() {
        return html`${this.#renderItems()} ${this.#renderAddButton()}`;
    }

    #renderAddButton() {
        if (this.selection.length >= 1) return nothing;
        return html`
            <uui-button
                id="btn-add"
                look="placeholder"
                href=${ifDefined(this._modalRoute)}
                label=${this.localize.term("general_choose")}
            ></uui-button>
        `;
    }

    #renderItems() {
        if (!this._statuses) return nothing;
        return html`
            <uui-ref-list>
                ${repeat(
                    this._statuses,
                    (status) => status.unique,
                    (status) => {
                        const item = this._items?.find((x) => x.unique === status.unique);
                        const isError = status.state.type === "error";
                        return html`
                            <umb-entity-item-ref
                                id=${status.unique}
                                .item=${item}
                                ?error=${isError}
                                .errorMessage=${status.state.error}
                                .errorDetail=${isError ? status.unique : undefined}
                                standalone
                            >
                                <uui-action-bar slot="actions">
                                    <uui-button
                                        label=${this.localize.term("general_remove")}
                                        @click=${() => this.#pickerContext.requestRemoveItem(status.unique)}
                                    ></uui-button>
                                </uui-action-bar>
                            </umb-entity-item-ref>
                        `;
                    },
                )}
            </uui-ref-list>
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

export default UaInputServiceAccountElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-input-service-account": UaInputServiceAccountElement;
    }
}
