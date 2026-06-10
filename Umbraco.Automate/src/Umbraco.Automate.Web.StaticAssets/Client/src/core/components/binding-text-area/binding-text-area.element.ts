import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import type { UaBindingInsertable } from "../binding-text-box/binding-editor.types.js";

/**
 * A textarea property editor for binding-enabled fields.
 * Identical to the built-in TextArea but registered under our own alias so
 * the "Insert binding" property action targets only these fields.
 */
@customElement("ua-binding-text-area")
export class UaBindingTextAreaElement
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
    private _rows = 6;

    /**
     * Last known caret position on the underlying textarea, captured on blur so
     * it survives the focus loss caused by opening the property-action popover
     * and the binding-picker modal.
     */
    #lastSelection: { start: number; end: number } | null = null;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        const rows = config.getValueByAlias<number>("rows");
        if (rows) this._rows = rows;
    }

    protected override async firstUpdated(): Promise<void> {
        const uuiTextarea = this.shadowRoot!.querySelector("uui-textarea")!;
        this.addFormControlElement(uuiTextarea);

        // uui-textarea renders its inner native <textarea> on its own first update.
        await uuiTextarea.updateComplete;

        const nativeTextarea = this.#getNativeTextarea();
        if (!nativeTextarea) return;

        nativeTextarea.addEventListener("blur", () => {
            this.#lastSelection = {
                start: nativeTextarea.selectionStart ?? nativeTextarea.value.length,
                end: nativeTextarea.selectionEnd ?? nativeTextarea.value.length,
            };
        });
    }

    override focus() {
        return this.shadowRoot?.querySelector<HTMLElement>("uui-textarea")?.focus();
    }

    /**
     * Splice `expression` into the current value at the last known caret
     * position. Falls back to appending if no caret has been captured yet.
     */
    public insertAtCaret(expression: string): void {
        const currentValue = (this.value as string) ?? "";
        const nativeTextarea = this.#getNativeTextarea();

        const { start, end } = this.#resolveInsertRange(currentValue, nativeTextarea);
        const newValue = currentValue.slice(0, start) + expression + currentValue.slice(end);
        const newCaret = start + expression.length;

        this.value = newValue;
        this.#lastSelection = { start: newCaret, end: newCaret };
        this.dispatchEvent(new UmbChangeEvent());

        this.updateComplete.then(() => {
            const textarea = this.#getNativeTextarea();
            if (!textarea) return;
            textarea.focus();
            textarea.setSelectionRange(newCaret, newCaret);
        });
    }

    /**
     * Reaches into `uui-textarea` for its underlying native textarea so we can
     * read and restore selection. The `_textarea` field is part of UUI's
     * element today but is not a documented public API — guard against it
     * disappearing.
     */
    #getNativeTextarea(): HTMLTextAreaElement | undefined {
        const uuiTextarea = this.shadowRoot?.querySelector("uui-textarea");
        return (uuiTextarea as unknown as { _textarea?: HTMLTextAreaElement } | null)?._textarea;
    }

    #resolveInsertRange(
        currentValue: string,
        nativeTextarea: HTMLTextAreaElement | undefined,
    ): { start: number; end: number } {
        if (this.#lastSelection) return this.#lastSelection;
        if (nativeTextarea && nativeTextarea.selectionStart != null) {
            return {
                start: nativeTextarea.selectionStart,
                end: nativeTextarea.selectionEnd ?? nativeTextarea.selectionStart,
            };
        }
        return { start: currentValue.length, end: currentValue.length };
    }

    #onInput(e: InputEvent) {
        const newValue = (e.target as HTMLTextAreaElement).value;
        if (newValue === this.value) return;
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <uui-textarea
                .label=${this.name ?? ""}
                .value=${this.value ?? ""}
                .rows=${this._rows}
                .requiredMessage=${this.mandatoryMessage}
                ?readonly=${this.readonly}
                ?required=${this.mandatory}
                @input=${this.#onInput}
            >
            </uui-textarea>
        `;
    }

    static override styles = [
        css`
            uui-textarea {
                width: 100%;
            }
        `,
    ];
}

export default UaBindingTextAreaElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-text-area": UaBindingTextAreaElement;
    }
}
