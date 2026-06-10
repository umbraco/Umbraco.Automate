import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UUIInputElement } from "@umbraco-cms/backoffice/external/uui";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import type { UaBindingInsertable } from "./binding-editor.types.js";

/**
 * A text box property editor for binding-enabled fields.
 * Identical to the built-in TextBox but registered under our own alias so
 * the "Insert binding" property action targets only these fields.
 *
 * The binding picker is provided by a `propertyAction` manifest, not inline.
 */
@customElement("ua-binding-text-box")
export class UaBindingTextBoxElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement, UaBindingInsertable
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: String })
    name?: string;

    @state()
    private _placeholder?: string;

    /**
     * Last known caret position on the underlying input, captured on blur so it
     * survives the focus loss caused by opening the property-action popover
     * and the binding-picker modal.
     */
    #lastSelection: { start: number; end: number } | null = null;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._placeholder = config.getValueByAlias<string>("placeholder") ?? "";
    }

    protected override async firstUpdated(): Promise<void> {
        const uuiInput = this.shadowRoot!.querySelector<UUIInputElement>("uui-input")!;
        this.addFormControlElement(uuiInput);

        // uui-input renders its inner native <input> on its own first update.
        await uuiInput.updateComplete;

        const nativeInput = this.#getNativeInput();
        if (!nativeInput) return;

        nativeInput.addEventListener("blur", () => {
            this.#lastSelection = {
                start: nativeInput.selectionStart ?? nativeInput.value.length,
                end: nativeInput.selectionEnd ?? nativeInput.value.length,
            };
        });
    }

    override focus() {
        return this.shadowRoot?.querySelector<UUIInputElement>("uui-input")?.focus();
    }

    /**
     * Splice `expression` into the current value at the last known caret
     * position. Falls back to appending if no caret has been captured yet.
     */
    public insertAtCaret(expression: string): void {
        const currentValue = (this.value as string) ?? "";
        const nativeInput = this.#getNativeInput();

        const { start, end } = this.#resolveInsertRange(currentValue, nativeInput);
        const newValue = currentValue.slice(0, start) + expression + currentValue.slice(end);
        const newCaret = start + expression.length;

        this.value = newValue;
        this.#lastSelection = { start: newCaret, end: newCaret };
        this.dispatchEvent(new UmbChangeEvent());

        // Wait for the re-render to push the new value into the native input
        // before restoring the caret.
        this.updateComplete.then(() => {
            const input = this.#getNativeInput();
            if (!input) return;
            input.focus();
            input.setSelectionRange(newCaret, newCaret);
        });
    }

    /**
     * Reaches into `uui-input` for its underlying native input so we can read
     * and restore selection. The `_input` field is part of UUI's element today
     * but is not a documented public API — guard against it disappearing.
     */
    #getNativeInput(): HTMLInputElement | undefined {
        const uuiInput = this.shadowRoot?.querySelector<UUIInputElement>("uui-input");
        return (uuiInput as unknown as { _input?: HTMLInputElement } | undefined)?._input;
    }

    #resolveInsertRange(
        currentValue: string,
        nativeInput: HTMLInputElement | undefined,
    ): { start: number; end: number } {
        if (this.#lastSelection) return this.#lastSelection;
        if (nativeInput && nativeInput.selectionStart != null) {
            return {
                start: nativeInput.selectionStart,
                end: nativeInput.selectionEnd ?? nativeInput.selectionStart,
            };
        }
        return { start: currentValue.length, end: currentValue.length };
    }

    #onInput(e: InputEvent) {
        const newValue = (e.target as HTMLInputElement).value;
        if (newValue === this.value) return;
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <uui-input
                .label=${this.name ?? ""}
                .placeholder=${this._placeholder ?? ""}
                .requiredMessage=${this.mandatoryMessage}
                .value=${this.value ?? ""}
                ?readonly=${this.readonly}
                ?required=${this.mandatory}
                @input=${this.#onInput}
            >
            </uui-input>
        `;
    }

    static override styles = [
        css`
            uui-input {
                width: 100%;
            }
        `,
    ];
}

export default UaBindingTextBoxElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-text-box": UaBindingTextBoxElement;
    }
}
