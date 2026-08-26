import { css, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { TemplateResult } from "@umbraco-cms/backoffice/external/lit";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import { UA_BINDING_PICKER_MODAL } from "../binding-text-box/binding-picker-modal.token.js";

/** Modes the editor can be in. Mirrors the two ways an entity key gets supplied. */
export type UaEntityKeyPickerMode = "pick" | "binding";

/**
 * Whether a stored value is a binding expression rather than a literal key.
 *
 * Deliberately loose: any `${ ... }` anywhere in the value counts. A key field holding
 * a partial or half-typed expression still belongs in binding mode, otherwise toggling
 * away from the tree picker would look like the value was discarded.
 */
export function isBindingExpression(value: unknown): boolean {
    return typeof value === "string" && /\$\{[^}]*\}/.test(value);
}

/**
 * Base for the entity key property editors (content, media). Settings fields that hold a
 * node key accept two very different things: a literal GUID the author picks off the tree,
 * and a `${ }` binding carrying a key from the trigger or an earlier step. This renders one
 * field that switches between them, so picking a node is the obvious default without taking
 * bindings away.
 *
 * Mode is derived from the value, not stored — a `${ }` value opens in binding mode, anything
 * else in picker mode. That keeps the stored settings shape unchanged (still a single string
 * the server parses with `Guid.TryParse`) and means automations authored before this editor
 * existed open in the right mode with no migration.
 *
 * Subclasses supply the entity-specific tree input. Binding mode and the toggle live here.
 */
export abstract class UaEntityKeyPickerElementBase
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    @property({ type: Boolean })
    mandatory?: boolean;

    @property({ type: String })
    mandatoryMessage = UMB_VALIDATION_EMPTY_LOCALIZATION_KEY;

    @property({ type: String })
    name?: string;

    /** Binding sources injected into the property config by `ua-settings-form`. */
    @state()
    protected _bindingSources: BindingSource[] = [];

    /**
     * Mode the author explicitly switched to. Undefined until they touch the toggle,
     * at which point it overrides the mode derived from the value — otherwise clearing
     * a picked node would bounce a deliberate binding field back to the tree picker.
     */
    @state()
    private _chosenMode?: UaEntityKeyPickerMode;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._bindingSources = config.getValueByAlias<BindingSource[]>("bindingSources") ?? [];
    }

    constructor() {
        super();

        // Validate on the host rather than registering the inner control, so the mandatory
        // message survives a mode switch swapping one inner element for another.
        this.addValidator(
            "valueMissing",
            () => this.mandatoryMessage,
            () => !!this.mandatory && !this.value,
        );
    }

    protected get mode(): UaEntityKeyPickerMode {
        return this._chosenMode ?? (isBindingExpression(this.value) ? "binding" : "pick");
    }

    /** The literal key to hand the tree input. A binding is not one, so it passes nothing. */
    protected get pickerValue(): string {
        return isBindingExpression(this.value) ? "" : (this.value ?? "");
    }

    #setValue(value: string) {
        if (value === this.value) return;
        this.value = value;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #switchMode(mode: UaEntityKeyPickerMode) {
        this._chosenMode = mode;

        // A binding can't be shown on a tree, so switching to the picker drops it. The
        // reverse is kept: a GUID sitting in the expression box is still a valid value and
        // gives the author something to edit rather than an empty field.
        if (mode === "pick" && isBindingExpression(this.value)) {
            this.#setValue("");
        }
    }

    /**
     * Reads the key off the entity input's own change event. Both `umb-input-document` and
     * `umb-input-media` emit a comma-separated selection; these fields hold one key, so the
     * inputs are capped at one and the whole value is the key.
     */
    protected onPickerChange(event: Event) {
        const target = event.target as HTMLElement & { value?: string };
        this.#setValue(target.value ?? "");
    }

    #onExpressionInput(event: InputEvent) {
        this.#setValue((event.target as HTMLInputElement).value);
    }

    /**
     * Opens the same binding picker the "Insert binding" property action uses, so both routes
     * to an expression look and behave alike. The selection replaces the value rather than
     * splicing at the caret: the field holds exactly one key, so appending a second expression
     * to the first could never resolve to a valid GUID.
     */
    async #openBindingPicker() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_BINDING_PICKER_MODAL, {
            data: { sources: this._bindingSources },
        });

        try {
            const { expression } = await modal.onSubmit();
            this.#setValue(expression);
        } catch {
            // Modal dismissed.
        }
    }

    /** Renders the entity-specific tree input, wired to `onPickerChange`. */
    protected abstract renderPicker(): TemplateResult;

    /**
     * The toggle is pointless with nothing to bind to — a trigger's own settings, or a step
     * with no predecessors — so it only appears where bindings are actually in scope. A field
     * already holding an expression keeps the toggle regardless, so the author is never
     * stranded in a mode they can't leave.
     */
    #canBind(): boolean {
        return this._bindingSources.length > 0 || isBindingExpression(this.value);
    }

    override render() {
        return html`
            <div id="wrapper">
                ${this.#renderModeToggle()}
                ${this.mode === "binding"
                    ? this.#renderExpression()
                    : html`<div id="picker">${this.renderPicker()}</div>`}
            </div>
        `;
    }

    #renderExpression() {
        return html`
            <div id="expression">
                <uui-input
                    .label=${this.name ?? ""}
                    .value=${this.value ?? ""}
                    placeholder=${this.localize.term("uaEntityPicker_expressionPlaceholder")}
                    ?readonly=${this.readonly}
                    @input=${this.#onExpressionInput}
                >
                </uui-input>
                <uui-button
                    id="insert"
                    compact
                    look="secondary"
                    label=${this.localize.term("uaBindings_insertExpression")}
                    ?disabled=${this.readonly || this._bindingSources.length === 0}
                    @click=${this.#openBindingPicker}
                >
                    <uui-icon name="icon-code"></uui-icon>
                </uui-button>
            </div>
        `;
    }

    #onToggleChange(event: CustomEvent) {
        const checked = (event.target as HTMLElement & { checked?: boolean }).checked;
        this.#switchMode(checked ? "binding" : "pick");
    }

    /**
     * A labelled switch above the input, the shape Forms uses for the submit message format.
     *
     * Both label slots carry the same string on purpose. `umb-input-toggle` swaps the text with
     * the state, which reads badly for a mode: a command like "Use a binding expression" next to
     * an already-on switch could equally be describing the mode or offering it, and the text
     * moving on click leaves nothing fixed to read the switch against. One unchanging statement
     * with the switch carrying yes/no is the checkbox convention, and it has neither problem.
     */
    #renderModeToggle() {
        if (this.readonly || !this.#canBind()) return nothing;

        const label = this.localize.term("uaEntityPicker_useBinding");

        return html`
            <umb-input-toggle
                id="toggle"
                .showLabels=${true}
                .labelOff=${label}
                .labelOn=${label}
                ?checked=${this.mode === "binding"}
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

            /* The tree inputs size to their content, which leaves a picked node as a short card
               floating in a wide field. Stretch the input and whatever it renders to the field. */
            #picker,
            #picker > * {
                width: 100%;
            }

            #expression {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
                width: 100%;
            }

            #expression uui-input {
                flex: 1;
                font-family: var(--uui-font-monospace, monospace);
            }

            #insert {
                --uui-button-height: 28px;
            }

            #toggle {
                align-self: flex-start;
                font-size: var(--uui-type-small-size, 12px);
            }
        `,
    ];
}
