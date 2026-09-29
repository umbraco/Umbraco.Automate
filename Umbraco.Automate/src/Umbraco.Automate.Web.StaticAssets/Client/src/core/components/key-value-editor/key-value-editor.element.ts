import { css, html, customElement, property, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import "../binding-picker/binding-picker-button.element.js";

/**
 * One row of a key/value list. Serialized as-is, and read back into a settings POCO
 * case-insensitively, so the casing here is a presentation detail rather than a contract.
 */
export interface KeyValueRow {
    key: string;
    value: string | null;
}

function createEmptyRow(): KeyValueRow {
    return { key: "", value: "" };
}

/**
 * Edits a list of key/value pairs as rows — HTTP headers, form fields, query parameters —
 * rather than as hand-written JSON, where a typo is invisible until the request goes out
 * without them. Each value gets a binding picker, so `${ }` expressions can be inserted
 * from the available trigger and step outputs instead of typed from memory.
 */
@customElement("ua-key-value-editor")
export class UaKeyValueEditorElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property({ attribute: false })
    value: KeyValueRow[] = [];

    @property({ type: Array })
    bindingSources: BindingSource[] = [];

    @property({ type: String })
    keyPlaceholder?: string;

    @property({ type: String })
    valuePlaceholder?: string;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        const sources = config.getValueByAlias<BindingSource[]>("bindingSources");
        if (sources) {
            this.bindingSources = sources;
        }

        this.keyPlaceholder = config.getValueByAlias<string>("keyPlaceholder") ?? this.keyPlaceholder;
        this.valuePlaceholder = config.getValueByAlias<string>("valuePlaceholder") ?? this.valuePlaceholder;
    }

    #cloneValue(): KeyValueRow[] {
        return structuredClone(this.value ?? []);
    }

    #emitChange(newValue: KeyValueRow[]) {
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onKeyChange(rowIndex: number, e: Event) {
        const input = e.target as HTMLInputElement;
        const newValue = this.#cloneValue();
        newValue[rowIndex].key = input.value;
        this.#emitChange(newValue);
    }

    #onValueChange(rowIndex: number, e: Event) {
        const input = e.target as HTMLInputElement;
        const newValue = this.#cloneValue();
        newValue[rowIndex].value = input.value;
        this.#emitChange(newValue);
    }

    /**
     * Appends the picked expression to the row's value rather than replacing it, so a header
     * like `Bearer ${ ... }` can be built up from a literal prefix and a binding.
     */
    #onBindingSelect(rowIndex: number, e: CustomEvent<{ expression: string }>) {
        e.stopPropagation();
        const newValue = this.#cloneValue();
        newValue[rowIndex].value = `${newValue[rowIndex].value ?? ""}${e.detail.expression}`;
        this.#emitChange(newValue);
    }

    #addRow() {
        const newValue = this.#cloneValue();
        newValue.push(createEmptyRow());
        this.#emitChange(newValue);
    }

    #removeRow(rowIndex: number) {
        const newValue = this.#cloneValue();
        newValue.splice(rowIndex, 1);
        this.#emitChange(newValue);
    }

    #renderRow(row: KeyValueRow, rowIndex: number) {
        return html`
            <div class="row">
                <uui-input
                    class="key"
                    label=${this.localize.term("uaKeyValueEditor_key")}
                    placeholder=${this.keyPlaceholder ?? this.localize.term("uaKeyValueEditor_keyPlaceholder")}
                    .value=${row.key ?? ""}
                    @change=${(e: Event) => this.#onKeyChange(rowIndex, e)}
                ></uui-input>

                <div class="value">
                    <uui-input
                        label=${this.localize.term("uaKeyValueEditor_value")}
                        placeholder=${this.valuePlaceholder ?? this.localize.term("uaKeyValueEditor_valuePlaceholder")}
                        .value=${row.value ?? ""}
                        @change=${(e: Event) => this.#onValueChange(rowIndex, e)}
                    ></uui-input>
                    <ua-binding-picker-button
                        .sources=${this.bindingSources}
                        @ua:binding-select=${(e: CustomEvent<{ expression: string }>) =>
                            this.#onBindingSelect(rowIndex, e)}
                    ></ua-binding-picker-button>
                </div>

                <uui-button
                    look="secondary"
                    compact
                    label=${this.localize.term("uaKeyValueEditor_removeRow")}
                    @click=${() => this.#removeRow(rowIndex)}
                >
                    <uui-icon name="icon-trash"></uui-icon>
                </uui-button>
            </div>
        `;
    }

    override render() {
        const rows = this.value ?? [];

        return html`
            <div class="key-value-editor">
                ${rows.length > 0
                    ? repeat(
                          rows,
                          (_row, index) => index,
                          (row, index) => this.#renderRow(row, index),
                      )
                    : nothing}

                <uui-button
                    class="add-row-btn"
                    look="placeholder"
                    label=${this.localize.term("uaKeyValueEditor_addRow")}
                    @click=${() => this.#addRow()}
                >
                    ${this.localize.term("uaKeyValueEditor_addRow")}
                </uui-button>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            .key-value-editor {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-2);
            }

            .row {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
            }

            .row .key {
                flex: 1 1 35%;
                min-width: 0;
            }

            .row .value {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-1);
                flex: 1 1 65%;
                min-width: 0;
            }

            .row .value uui-input {
                flex: 1;
                min-width: 0;
            }

            .add-row-btn {
                margin-top: var(--uui-size-space-1);
            }
        `,
    ];
}

export default UaKeyValueEditorElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-key-value-editor": UaKeyValueEditorElement;
    }
}
