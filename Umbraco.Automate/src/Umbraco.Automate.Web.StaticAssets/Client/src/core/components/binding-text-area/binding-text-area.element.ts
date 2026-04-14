import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";

/**
 * A textarea property editor for binding-enabled fields.
 * Identical to the built-in TextArea but registered under our own alias so
 * the "Insert binding" property action targets only these fields.
 */
@customElement("ua-binding-text-area")
export class UaBindingTextAreaElement
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

    @state()
    private _rows = 6;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        const rows = config.getValueByAlias<number>("rows");
        if (rows) this._rows = rows;
    }

    protected override firstUpdated(): void {
        this.addFormControlElement(this.shadowRoot!.querySelector("uui-textarea")!);
    }

    override focus() {
        return this.shadowRoot?.querySelector<HTMLElement>("uui-textarea")?.focus();
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
