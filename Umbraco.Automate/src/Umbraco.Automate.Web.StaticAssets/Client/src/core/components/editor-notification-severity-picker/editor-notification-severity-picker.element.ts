import { css, customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";

const SEVERITIES = ["Default", "Positive", "Warning", "Danger"] as const;
const DEFAULT_SEVERITY = "Default";

@customElement("ua-editor-notification-severity-picker")
export class UaEditorNotificationSeverityPickerElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    #selfUpdated = false;

    protected override updated() {
        if (this.#selfUpdated) {
            this.#selfUpdated = false;
            return;
        }
        if (!this.value && !this.readonly) {
            this.#selfUpdated = true;
            this.value = DEFAULT_SEVERITY;
            this.dispatchEvent(new UmbChangeEvent());
        }
    }

    #onChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        if (next === this.value) return;
        this.#selfUpdated = true;
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        const options = SEVERITIES.map((s) => ({
            name: s,
            value: s,
            selected: s === this.value,
        }));

        return html`
            <uui-select
                label="Severity"
                .options=${options}
                ?disabled=${this.readonly}
                @change=${this.#onChange}
            ></uui-select>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            uui-select {
                width: 100%;
            }
        `,
    ];
}

export default UaEditorNotificationSeverityPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-editor-notification-severity-picker": UaEditorNotificationSeverityPickerElement;
    }
}
