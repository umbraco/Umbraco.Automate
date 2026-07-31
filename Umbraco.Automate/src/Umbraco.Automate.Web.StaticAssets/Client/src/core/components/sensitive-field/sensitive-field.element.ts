import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UUIInputPasswordElement } from "@umbraco-cms/backoffice/external/uui";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";

/**
 * Whether a value is a configuration reference rather than a literal secret.
 *
 * Mirrors `ConfigurationReferenceScanner.IsWholeReference` on the server: a `$` followed by
 * one or more key characters, with nothing else around it. `$$` collapses to a literal `$`,
 * so it fails the key-character test and stays masked — a literal is still a secret.
 *
 * The server additionally gates references on an allow-list of key prefixes
 * (`Umbraco:Automate:Secrets`, `Umbraco:Automate:Variables` by default) which the client has
 * no way to know. A whole-value `$Something` outside that allow-list is therefore treated as
 * a literal by the resolver but shown revealed here. That is an accepted gap: this guards
 * against reading over a shoulder, and a literal secret shaped exactly like a config key is
 * not a case worth shipping the allow-list to the browser for.
 */
function isConfigReference(value: unknown): boolean {
    return typeof value === "string" && /^\$[A-Za-z0-9_.:-]+$/.test(value);
}

/**
 * Property editor UI for fields marked `[Field(IsSensitive = true)]`. Renders
 * `uui-input-password`, which masks the value and carries its own reveal toggle, so
 * credentials are not left on screen during demos and screen shares.
 *
 * The alias is chosen server-side by `EditableModelSchemaBuilder`, so `isSensitive` never has
 * to drive rendering on the client. An explicit `EditorUiAlias` on the attribute still wins,
 * which is the escape hatch for a sensitive field masking would make unusable.
 *
 * Scope: the value still reaches the browser in full and is readable in dev tools. This is a
 * shoulder-surfing guard, not a disclosure control.
 *
 * Configuration references (`$Umbraco:Automate:Secrets:ApiKey`) render revealed. They are
 * pointers, not secrets, and hiding them makes it impossible to see which key a field uses.
 * This drives `uui-input-password`'s `type` rather than swapping in a plain input for those,
 * because swapping destroys and recreates the element, taking the caret with it mid-edit.
 * Lit only writes the property when the bound value changes, so a reveal the user toggled by
 * hand survives further typing.
 */
@customElement("ua-sensitive-field")
export class UaSensitiveFieldElement
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
    private _placeholder?: string;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._placeholder = config.getValueByAlias<string>("placeholder") ?? "";
    }

    protected override firstUpdated(): void {
        this.addFormControlElement(this.shadowRoot!.querySelector<UUIInputPasswordElement>("uui-input-password")!);
    }

    override focus() {
        return this.shadowRoot?.querySelector<UUIInputPasswordElement>("uui-input-password")?.focus();
    }

    #onInput(e: InputEvent) {
        const newValue = (e.target as HTMLInputElement).value;
        if (newValue === this.value) return;
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        const type = isConfigReference(this.value) ? "text" : "password";

        return html`
            <uui-input-password
                .label=${this.name ?? ""}
                .placeholder=${this._placeholder ?? ""}
                .requiredMessage=${this.mandatoryMessage}
                .type=${type}
                .value=${this.value ?? ""}
                ?readonly=${this.readonly}
                ?required=${this.mandatory}
                autocomplete="off"
                @input=${this.#onInput}
            >
            </uui-input-password>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            uui-input-password {
                width: 100%;
            }
        `,
    ];
}

export default UaSensitiveFieldElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-sensitive-field": UaSensitiveFieldElement;
    }
}
