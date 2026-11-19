import { css, customElement, html, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UMB_VALIDATION_EMPTY_LOCALIZATION_KEY, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type {
    UmbPropertyEditorUiElement,
    UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import { UA_BINDING_PICKER_MODAL } from "../binding-text-box/binding-picker-modal.token.js";
import { isEmailAddress, joinAddresses, splitAddresses } from "../input-email-address/email-address.utils.js";
import type { UaInputEmailAddressElement } from "../input-email-address/input-email-address.element.js";
import "../input-email-address/input-email-address.element.js";

const isBinding = (entry: string) => entry.includes("${");

/**
 * Property editor for Send Email's address fields. The tag editing lives in the generic
 * `ua-input-email-address`; this adds what is Automate's own: the comma-separated string the
 * settings store, so existing automations need no migration, and `${ }` bindings, which get a
 * picker and a tag of their own and are resolved at run time.
 */
@customElement("ua-email-address-input")
export class UaEmailAddressInputElement
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
    private _bindingSources: BindingSource[] = [];

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._bindingSources = config.getValueByAlias<BindingSource[]>("bindingSources") ?? [];
    }

    protected override firstUpdated() {
        this.addFormControlElement(this.#input()!);
    }

    override focus() {
        this.#input()?.focus();
    }

    #input() {
        return this.shadowRoot?.querySelector<UaInputEmailAddressElement>("ua-input-email-address");
    }

    #onChange(e: UmbChangeEvent) {
        e.stopPropagation();
        this.value = joinAddresses((e.target as UaInputEmailAddressElement).items);
        this.dispatchEvent(new UmbChangeEvent());
    }

    async #insertBinding() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_BINDING_PICKER_MODAL, {
            data: { sources: this._bindingSources },
        });

        try {
            const { expression } = await modal.onSubmit();
            this.#input()?.insert(expression);
        } catch {
            // dismissed
        }
    }

    override render() {
        return html`
            <ua-input-email-address
                .items=${splitAddresses(this.value as string | undefined)}
                .label=${this.name}
                .validate=${(entry: string) => isBinding(entry) || isEmailAddress(entry)}
                .itemIcon=${(entry: string) => (isBinding(entry) ? "icon-code" : undefined)}
                .requiredMessage=${this.mandatoryMessage}
                ?required=${this.mandatory}
                ?readonly=${this.readonly}
                @change=${this.#onChange}
            >
                ${this._bindingSources.length > 0 && !this.readonly
                    ? html`<uui-button
                          slot="actions"
                          look="secondary"
                          compact
                          label=${this.localize.term("uaBindings_insertExpression")}
                          title=${this.localize.term("uaBindings_insertExpression")}
                          @click=${this.#insertBinding}
                      >
                          <uui-icon name="icon-code"></uui-icon>
                      </uui-button>`
                    : nothing}
            </ua-input-email-address>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }
        `,
    ];
}

export default UaEmailAddressInputElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-email-address-input": UaEmailAddressInputElement;
    }
}
