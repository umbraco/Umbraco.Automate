import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import "./binding-picker.element.js";

let popoverIdCounter = 0;

/**
 * A small button that opens a binding picker popover.
 * Place next to any input that accepts binding expressions.
 */
@customElement("ua-binding-picker-button")
export class UaBindingPickerButtonElement extends UmbLitElement {
    @property({ type: Array })
    sources: BindingSource[] = [];

    @state()
    private _popoverId = `ua-binding-popover-${++popoverIdCounter}`;

    override render() {
        if (this.sources.length === 0) return html``;

        return html`
            <uui-button
                compact
                look="secondary"
                popovertarget=${this._popoverId}
                title="Insert binding expression"
            >
                <uui-icon name="icon-code"></uui-icon>
            </uui-button>
            <div id=${this._popoverId} popover class="popover">
                <ua-binding-picker .sources=${this.sources}></ua-binding-picker>
            </div>
        `;
    }

    static override styles = css`
        :host {
            display: inline-flex;
            align-items: center;
        }

        uui-button {
            --uui-button-height: 28px;
            --uui-button-font-size: 12px;
        }

        .popover {
            margin: 0;
            padding: var(--uui-size-space-3);
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            background: var(--uui-color-surface, #fff);
            box-shadow: var(--uui-shadow-depth-3, 0 3px 10px rgba(0, 0, 0, 0.2));
            max-height: 400px;
            overflow-y: auto;
        }
    `;
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-picker-button": UaBindingPickerButtonElement;
    }
}
