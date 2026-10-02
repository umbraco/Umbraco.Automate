import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import type { PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { createExtensionElement } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry } from "@umbraco-cms/backoffice/extension-registry";
import type {
    ManifestPropertyEditorUi,
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import type { UaBindingInsertable } from "../binding-text-box/binding-editor.types.js";
import type { UaBindingTextBoxElement } from "../binding-text-box/binding-text-box.element.js";
import "../binding-text-box/binding-text-box.element.js";
import {
    getBindingExpression,
    isEmptySettingsValue,
    toBindingValue,
    type UaBindingValueShape,
} from "./bindable-value.utils.js";
import { BINDABLE_EDITOR_CONFIG_ALIASES, BINDABLE_EDITOR_UI_ALIAS } from "./constants.js";

type UaBindableEditorMode = "editor" | "binding";

/** What `UmbFormControlMixin.addFormControlElement` accepts. */
type UaNativeFormControlElement = Pick<
    HTMLObjectElement,
    "validity" | "checkValidity" | "validationMessage" | "setCustomValidity"
> &
    HTMLElement;

/** `umb-property` still honours this deprecated event, so a wrapped editor may raise it. */
const LEGACY_PROPERTY_VALUE_CHANGE = "property-value-change";

/**
 * Wraps any property editor (a Forms form picker, a document picker, a dropdown) for a
 * `[Field(SupportsBindings = true)]` setting, so the field can hold either what that editor
 * produces or a `${ }` binding. `ua-settings-form` routes such fields here and names the
 * editor being wrapped in the config.
 *
 * Mode is derived from the value, not stored: a `${ }` value (or a one-item array holding one)
 * opens in binding mode, anything else in the wrapped editor. The stored settings shape is
 * unchanged, so automations saved before this editor existed open in the right mode with no
 * migration, and the server's binding resolver sees the same values it always has.
 *
 * Bindings are entered through the same "Insert binding" property action as the text fields,
 * which also switches the field into binding mode, plus a labelled switch for discovery and
 * for switching back.
 */
@customElement("ua-bindable-editor")
export class UaBindableEditorElement
    extends UmbFormControlMixin<unknown, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
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

    @property({ attribute: false })
    dataSourceAlias?: string;

    @state()
    private _bindingSources: BindingSource[] = [];

    /**
     * Mode the author explicitly switched to. Undefined until they touch the switch, at which
     * point it overrides the mode derived from the value, otherwise emptying the expression
     * box would bounce a deliberate binding field back to the picker.
     */
    @state()
    private _chosenMode?: UaBindableEditorMode;

    /** The wrapped editor, created from its manifest. Undefined while loading or when missing. */
    @state()
    private _editorElement?: UmbPropertyEditorUiElement;

    /** True once the wrapped alias is known not to be registered, e.g. its package is not installed. */
    @state()
    private _editorMissing = false;

    #config?: UmbPropertyEditorConfigCollection;
    #editorUiAlias?: string;
    #valueShape: UaBindingValueShape = "string";

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this.#config = config;
        this._bindingSources = config.getValueByAlias<BindingSource[]>("bindingSources") ?? [];

        if (config.getValueByAlias<UaBindingValueShape>(BINDABLE_EDITOR_CONFIG_ALIASES.valueShape) === "array") {
            this.#valueShape = "array";
        }

        // The wrapped editor reads the same collection, so it sees its own `editorConfig` entries.
        if (this._editorElement) {
            this._editorElement.config = config;
        }

        this.#observeEditorUi(config.getValueByAlias<string>(BINDABLE_EDITOR_CONFIG_ALIASES.editorUiAlias));
        this.requestUpdate("config");
    }

    public get config(): UmbPropertyEditorConfigCollection | undefined {
        return this.#config;
    }

    constructor() {
        super();

        // Validate on the host rather than through the inner controls alone, so the mandatory
        // message holds in both modes and survives one inner element being swapped for another.
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !!this.mandatory && isEmptySettingsValue(this.value),
        );
    }

    get #mode(): UaBindableEditorMode {
        if (this._editorMissing) return "binding";
        return this._chosenMode ?? (getBindingExpression(this.value) !== undefined ? "binding" : "editor");
    }

    #observeEditorUi(alias: string | undefined) {
        if (!alias || alias === this.#editorUiAlias || alias === BINDABLE_EDITOR_UI_ALIAS) return;
        this.#editorUiAlias = alias;

        this.observe(
            umbExtensionsRegistry.byTypeAndAlias("propertyEditorUi", alias),
            (manifest) => this.#createEditor(manifest as ManifestPropertyEditorUi | undefined),
            "_observeWrappedEditorUi",
        );
    }

    async #createEditor(manifest: ManifestPropertyEditorUi | undefined) {
        if (this._editorElement?.manifest === manifest && manifest) return;

        this.#destroyEditor();

        if (!manifest) {
            this._editorMissing = true;
            return;
        }

        const element = await createExtensionElement<UmbPropertyEditorUiElement>(manifest);
        if (!element) {
            this._editorMissing = true;
            return;
        }

        this._editorMissing = false;
        element.manifest = manifest;
        element.addEventListener("change", this.#onEditorChange);
        element.addEventListener(LEGACY_PROPERTY_VALUE_CHANGE, this.#onEditorChange);
        this._editorElement = element;
        this.#syncEditor();
    }

    #destroyEditor() {
        const element = this._editorElement;
        if (!element) return;

        element.removeEventListener("change", this.#onEditorChange);
        element.removeEventListener(LEGACY_PROPERTY_VALUE_CHANGE, this.#onEditorChange);
        if (this.#isFormControl(element)) {
            this.removeFormControlElement(element);
        }
        element.destroy?.();
        this._editorElement = undefined;
    }

    /**
     * Pushes everything `umb-property` would have set on the wrapped editor had it mounted it
     * directly. A binding is not something the editor can show, so it gets no value then.
     */
    #syncEditor() {
        const element = this._editorElement;
        if (!element) return;

        element.name = this.name;
        element.mandatory = this.mandatory;
        element.mandatoryMessage = this.mandatoryMessage;
        element.readonly = this.readonly;
        element.toggleAttribute("readonly", this.readonly);
        element.dataSourceAlias = this.dataSourceAlias;
        if (this.#config) element.config = this.#config;
        element.value = getBindingExpression(this.value) === undefined ? this.value : undefined;

        // The wrapped editor's own rules (e.g. a picker's min/max items) count only while it is
        // the editor in use; a binding is checked when the run resolves it, not here.
        if (this.#isFormControl(element)) {
            if (this.#mode === "editor") {
                this.addFormControlElement(element);
            } else {
                this.removeFormControlElement(element);
            }
        }
    }

    #isFormControl(
        element: UmbPropertyEditorUiElement,
    ): element is UmbPropertyEditorUiElement & UaNativeFormControlElement {
        return "checkValidity" in element && "validity" in element;
    }

    protected override willUpdate(changed: PropertyValues) {
        super.willUpdate(changed);

        if (Array.isArray(this.value)) {
            // Seen an array: the field stores arrays, so a binding must be stored as one too.
            this.#valueShape = "array";
        }

        if (
            changed.has("value") ||
            changed.has("name") ||
            changed.has("mandatory") ||
            changed.has("mandatoryMessage") ||
            changed.has("readonly") ||
            changed.has("dataSourceAlias") ||
            changed.has("_chosenMode")
        ) {
            this.#syncEditor();
        }
    }

    override destroy() {
        this.#destroyEditor();
        super.destroy();
    }

    override focus() {
        const target =
            this.#mode === "binding"
                ? this.shadowRoot?.querySelector<HTMLElement>("ua-binding-text-box")
                : this._editorElement;
        target?.focus();
    }

    #setValue(value: unknown) {
        if (value === this.value) return;
        this.value = value;
        this.dispatchEvent(new UmbChangeEvent());
    }

    /**
     * Inner change events stop here and the wrapper raises its own: `umb-property` only accepts
     * change events whose target is the editor it mounted, which is this element.
     */
    #onEditorChange = (event: Event) => {
        event.stopPropagation();
        const element = event.target as UmbPropertyEditorUiElement;
        this.#setValue(element.value);
    };

    #onExpressionChange(event: Event) {
        event.stopPropagation();
        const expression = ((event.target as UaBindingTextBoxElement).value as string | undefined) ?? "";
        // Pin the mode: clearing the box would otherwise derive "editor" from the empty value
        // and swap the box out from under the author mid-edit.
        this._chosenMode = "binding";
        this.#setValue(expression === "" ? undefined : toBindingValue(expression, this.#valueShape));
    }

    #switchMode(mode: UaBindableEditorMode) {
        this._chosenMode = mode;

        if (mode === "editor") {
            // A binding can't be shown in the editor, so switching back drops it.
            if (getBindingExpression(this.value) !== undefined) this.#setValue(undefined);
            return;
        }

        // A literal string (a picked GUID) is kept: it is still a valid value and gives the
        // author something to edit. Anything else (a selection array) has no text form.
        if (typeof this.value !== "string") this.#setValue(undefined);
    }

    #onToggleChange(event: Event) {
        event.stopPropagation();
        const checked = (event.target as HTMLElement & { checked?: boolean }).checked;
        this.#switchMode(checked ? "binding" : "editor");
    }

    /**
     * Called by the "Insert binding" property action. In binding mode the expression goes in at
     * the caret like any binding text box; from the editor it switches the field into binding
     * mode holding just that expression, since a binding cannot sit alongside a picked value.
     */
    public insertAtCaret(expression: string): void {
        if (this.#mode === "binding") {
            const textBox = this.shadowRoot?.querySelector<UaBindingTextBoxElement>("ua-binding-text-box");
            if (textBox) {
                textBox.insertAtCaret(expression);
                return;
            }
        }

        this._chosenMode = "binding";
        this.#setValue(toBindingValue(expression, this.#valueShape));
    }

    /**
     * The switch is pointless with nothing to bind to, so it only shows where bindings are in
     * scope. A field already holding an expression keeps it regardless, so the author is never
     * stranded in a mode they can't leave.
     */
    #canSwitch(): boolean {
        if (this.readonly || this._editorMissing) return false;
        return this._bindingSources.length > 0 || getBindingExpression(this.value) !== undefined;
    }

    override render() {
        return html`
            <div id="wrapper">
                ${this.#renderModeToggle()}
                ${this.#mode === "binding" ? this.#renderExpression() : this.#renderEditor()}
            </div>
        `;
    }

    #renderEditor() {
        return html`<div id="editor">${this._editorElement ?? nothing}</div>`;
    }

    #renderExpression() {
        return html`
            <ua-binding-text-box
                .name=${this.name}
                .value=${getBindingExpression(this.value) ?? (typeof this.value === "string" ? this.value : "")}
                .config=${this.#config}
                ?readonly=${this.readonly}
                @change=${this.#onExpressionChange}
            >
            </ua-binding-text-box>
        `;
    }

    /**
     * Both label slots carry the same string on purpose: a switch whose text changes with its
     * state reads badly for a mode. One fixed statement with the switch carrying yes/no is the
     * checkbox convention, and the switch is keyboard-operable with Space like any uui-toggle.
     */
    #renderModeToggle() {
        if (!this.#canSwitch()) return nothing;

        const label = this.localize.term("uaBindings_useBinding");

        return html`
            <umb-input-toggle
                id="toggle"
                .showLabels=${true}
                .labelOff=${label}
                .labelOn=${label}
                ?checked=${this.#mode === "binding"}
                @change=${this.#onToggleChange}
            >
            </umb-input-toggle>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            #wrapper {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-2);
            }

            #editor,
            ua-binding-text-box {
                width: 100%;
            }

            #toggle {
                align-self: flex-start;
                font-size: var(--uui-type-small-size, 12px);
            }
        `,
    ];
}

export default UaBindableEditorElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-bindable-editor": UaBindableEditorElement;
    }
}
