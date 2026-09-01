import { createRef, css, customElement, html, property, ref, state, styleMap } from "@umbraco-cms/backoffice/external/lit";
import type { Ref } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent, UmbInputEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import type { CodeEditorLanguage, UmbCodeEditorElement } from "@umbraco-cms/backoffice/code-editor";
// Side-effect import: registers the `umb-code-editor` custom element (Monaco-based).
// Not imported as a value anywhere below, so the bare specifier form is required —
// a type-only import would be elided by the compiler and skip registration.
import "@umbraco-cms/backoffice/code-editor";
import type { UaBindingInsertable } from "../binding-text-box/binding-editor.types.js";

/**
 * A Monaco-based code editor property editor for binding-enabled fields.
 *
 * Umbraco's native `Umb.PropertyEditorUi.CodeEditor` isn't reusable as a child
 * element from outside the core package — its element module isn't part of the
 * `@umbraco-cms/backoffice/code-editor` public export map (only the lower-level
 * `umb-code-editor` component, models, and the code-editor modal are). So this
 * renders `umb-code-editor` directly, mirroring the config handling of
 * `UmbPropertyEditorUICodeEditorElement` (language/height/lineNumbers/minimap/wordWrap),
 * and registers under our own alias so the "Insert binding" property action
 * targets only these fields.
 */
@customElement("ua-binding-code-editor")
export class UaBindingCodeEditorElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement, UaBindingInsertable
{
    #defaultLanguage: CodeEditorLanguage = "javascript";

    @property({ type: Boolean, reflect: true })
    readonly = false;

    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: String })
    name?: string;

    @state()
    private _language?: CodeEditorLanguage = this.#defaultLanguage;

    @state()
    private _height = 400;

    @state()
    private _lineNumbers = true;

    @state()
    private _minimap = true;

    @state()
    private _wordWrap = false;

    #codeEditorRef: Ref<UmbCodeEditorElement> = createRef();

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        const language = config.getValueByAlias<Array<CodeEditorLanguage> | CodeEditorLanguage | undefined>(
            "language",
        );
        this._language = Array.isArray(language) ? language[0] : language;

        this._height = Number(config.getValueByAlias("height")) || 400;
        this._lineNumbers = config.getValueByAlias("lineNumbers") ?? false;
        this._minimap = config.getValueByAlias("minimap") ?? false;
        this._wordWrap = config.getValueByAlias("wordWrap") ?? false;
    }

    constructor() {
        super();

        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !!this.mandatory && (!this.value || (this.value as string).length === 0),
        );
    }

    override focus() {
        // umb-code-editor doesn't expose a focus() of its own; the Monaco instance
        // it wraps does, once loaded (firstUpdated is async, so guard for that).
        this.#codeEditorRef.value?.editor?.monacoEditor?.focus();
    }

    /**
     * Inserts `expression` at Monaco's current cursor/selection via `umb-code-editor`'s
     * public `insert()`. Monaco retains its selection state even after losing DOM focus
     * (e.g. while the binding-picker modal is open), so — unlike the textarea/text-box
     * variants — no manual caret capture-on-blur is needed here.
     */
    public insertAtCaret(expression: string): void {
        this.#codeEditorRef.value?.insert(expression);
    }

    #onInput(event: Event) {
        if (!(event instanceof UmbInputEvent)) return;
        const target = event.target as UmbCodeEditorElement;
        this.value = target.code;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return html`
            <umb-code-editor
                ${ref(this.#codeEditorRef)}
                style=${styleMap({ height: `${this._height}px` })}
                .label=${this.name ?? ""}
                .language=${this._language ?? this.#defaultLanguage}
                .code=${(this.value as string) ?? ""}
                ?disable-line-numbers=${!this._lineNumbers}
                ?disable-minimap=${!this._minimap}
                ?word-wrap=${this._wordWrap}
                ?readonly=${this.readonly}
                @input=${this.#onInput}
            >
            </umb-code-editor>
        `;
    }

    static override styles = [
        css`
            umb-code-editor {
                border-radius: var(--uui-border-radius);
                border: 1px solid var(--uui-color-divider-emphasis);
            }
        `,
    ];
}

export default UaBindingCodeEditorElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-code-editor": UaBindingCodeEditorElement;
    }
}
