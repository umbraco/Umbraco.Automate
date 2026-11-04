import { css, customElement, html, live, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { TYPED_SEPARATORS, isEmailAddress, joinAddresses, splitAddresses, splitList } from "./email-address.utils.js";

/**
 * A list of email addresses edited the way an email client's To field works: each address is a
 * tag, and Enter, a comma or a semicolon turns what has been typed into one. Backspace in an
 * empty input, or a click on a tag, puts that address back into the input for editing.
 *
 * Deliberately knows nothing about Automate, so it can move to the backoffice as it is. Callers
 * shape it through `validate` and `itemIcon`, and put buttons of their own in the `actions` slot.
 *
 * @slot actions - Buttons shown next to the field, e.g. a picker that calls `insert()`.
 * @fires change - When an address is added, removed or edited.
 */
@customElement("ua-input-email-address")
export class UaInputEmailAddressElement extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(
    UmbLitElement,
    undefined,
) {
    /** The addresses. `value` holds the same list, comma-separated. */
    @property({ type: Array })
    public get items(): string[] {
        return this._items;
    }
    public set items(items: string[] | undefined) {
        this._items = items ?? [];
        this.value = joinAddresses(this._items);
    }

    @state()
    private _items: string[] = [];

    @property({ type: String })
    label?: string;

    @property({ type: String })
    placeholder?: string;

    @property({ type: Boolean, reflect: true })
    readonly = false;

    @property({ type: Boolean })
    required = false;

    @property({ type: String })
    requiredMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    /** Marks a tag as invalid when this returns false. Defaults to {@link isEmailAddress}. */
    @property({ attribute: false })
    validate: (entry: string) => boolean = isEmailAddress;

    /** An icon for a tag that should stand out, such as a placeholder filled in later. */
    @property({ attribute: false })
    itemIcon?: (entry: string) => string | undefined;

    @state()
    private _draft = "";

    /** Where the address being edited came from, so it goes back in the same place. */
    #editIndex: number | null = null;

    constructor() {
        super();

        this.addValidator(
            "valueMissing",
            () => this.requiredMessage,
            () => this.required && this._items.length === 0,
        );
    }

    override focus() {
        this.shadowRoot?.querySelector<HTMLInputElement>("#input")?.focus();
    }

    /**
     * Adds `text` as an address of its own, or appends it to the one being typed, so a picked
     * value can also become part of a longer address.
     */
    public insert(text: string) {
        if (this._draft) {
            this._draft += text;
            this.updateComplete.then(() => this.focus());
            return;
        }
        this.#editIndex = null;
        this.#addItems([text]);
    }

    #setItems(items: string[]) {
        this.items = items;
        this.pristine = false;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #addItems(added: string[]) {
        if (added.length === 0) return;
        const items = [...this._items];
        items.splice(this.#editIndex ?? items.length, 0, ...added);
        if (this.#editIndex !== null) this.#editIndex += added.length;
        this.#setItems(items);
    }

    #commitDraft() {
        const typed = splitAddresses(this._draft, TYPED_SEPARATORS);
        this._draft = "";
        this.#addItems(typed);
        this.#editIndex = null;
    }

    #onInput(e: InputEvent) {
        e.stopPropagation();
        const parts = splitList((e.target as HTMLInputElement).value, TYPED_SEPARATORS);

        // A separator typed or pasted outside quotes ends an address, so everything before the
        // last one becomes a tag and the rest stays in the input.
        this._draft = parts.pop()!.trimStart();
        this.#addItems(parts.map((part) => part.trim()).filter((part) => part.length > 0));
    }

    #onKeydown(e: KeyboardEvent) {
        if (e.key === "Enter") {
            e.preventDefault();
            this.#commitDraft();
            return;
        }

        if (e.key === "Backspace" && this._draft === "" && this._items.length > 0) {
            e.preventDefault();
            this.#editItem(this._items.length - 1);
        }
    }

    #editItem(index: number) {
        const items = [...this._items];
        const [item] = items.splice(index, 1);
        this.#setItems(items);
        this._draft = item;
        this.#editIndex = index;
        this.updateComplete.then(() => this.focus());
    }

    #removeItem(index: number) {
        this.#setItems(this._items.filter((_item, i) => i !== index));
    }

    #renderTag(item: string, index: number) {
        const icon = this.itemIcon?.(item);
        const valid = this.validate(item);

        return html`
            <uui-tag
                look=${icon ? "outline" : "secondary"}
                color=${valid ? "default" : "danger"}
                title=${valid ? item : this.localize.term("uaInputEmailAddress_invalid")}
            >
                ${icon ? html`<uui-icon name=${icon}></uui-icon>` : nothing}
                ${this.readonly
                    ? html`<span class="text">${item}</span>`
                    : html`
                          <button
                              type="button"
                              class="text"
                              aria-label=${this.localize.term("uaInputEmailAddress_edit", item)}
                              @click=${() => this.#editItem(index)}
                          >
                              ${item}
                          </button>
                          <button
                              type="button"
                              class="remove"
                              aria-label=${this.localize.term("uaInputEmailAddress_remove", item)}
                              @click=${() => this.#removeItem(index)}
                          >
                              <uui-icon name="icon-wrong"></uui-icon>
                          </button>
                      `}
            </uui-tag>
        `;
    }

    override render() {
        return html`
            <div id="field">
                <div id="tags" @click=${() => this.focus()}>
                    ${repeat(
                        this._items,
                        (_item, index) => index,
                        (item, index) => this.#renderTag(item, index),
                    )}
                    ${this.readonly
                        ? nothing
                        : html`<input
                              id="input"
                              autocomplete="off"
                              aria-label=${this.label ?? this.localize.term("uaInputEmailAddress_label")}
                              placeholder=${this._items.length === 0
                                  ? (this.placeholder ?? this.localize.term("uaInputEmailAddress_placeholder"))
                                  : ""}
                              .value=${live(this._draft)}
                              @input=${this.#onInput}
                              @keydown=${this.#onKeydown}
                              @blur=${this.#commitDraft}
                          />`}
                </div>
                <slot name="actions"></slot>
            </div>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            #field {
                display: flex;
                align-items: flex-start;
                gap: var(--uui-size-space-1);
            }

            #tags {
                box-sizing: border-box;
                flex: 1;
                min-width: 0;
                display: flex;
                flex-wrap: wrap;
                align-items: center;
                gap: var(--uui-size-space-1);
                min-height: var(--uui-input-height, var(--uui-size-11));
                padding: var(--uui-size-space-1) var(--uui-size-space-2);
                border: 1px solid var(--uui-input-border-color, var(--uui-color-border));
                border-radius: var(--uui-border-radius);
                background-color: var(--uui-input-background-color, var(--uui-color-surface));
                cursor: text;
            }

            #tags:hover {
                border-color: var(--uui-input-border-color-hover, var(--uui-color-border-standalone));
            }

            #tags:focus-within {
                border-color: var(--uui-input-border-color-focus, var(--uui-color-border-emphasis));
                outline: calc(2px * var(--uui-show-focus-outline, 1)) solid var(--uui-color-focus);
            }

            #input {
                flex: 1;
                min-width: 8ch;
                height: var(--uui-size-8);
                padding: 0 var(--uui-size-space-1);
                border: none;
                outline: none;
                background: transparent;
                color: inherit;
                font: inherit;
            }

            uui-tag {
                max-width: 100%;
                gap: var(--uui-size-space-1);
            }

            button {
                padding: 0;
                border: none;
                background: none;
                color: inherit;
                font: inherit;
                cursor: pointer;
            }

            .text {
                max-width: 40ch;
                overflow: hidden;
                text-overflow: ellipsis;
                white-space: nowrap;
            }

            .remove {
                display: inline-flex;
                margin-left: var(--uui-size-space-1);
            }
        `,
    ];
}

export default UaInputEmailAddressElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-input-email-address": UaInputEmailAddressElement;
    }
}
