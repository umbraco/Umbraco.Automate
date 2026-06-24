import { html, customElement, property, nothing, when, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UA_AUTOMATION_ICON } from "../constants.js";
import type { UaAutomationSearchItemModel } from "./types.js";

@customElement("ua-automation-search-result-item")
export class UaAutomationSearchResultItemElement extends UmbLitElement {
    @property({ type: Object })
    item?: UaAutomationSearchItemModel;

    #statusColor(status: string): string {
        switch (status) {
            case "Published":
                return "positive";
            case "Draft":
                return "warning";
            case "Unpublished":
                return "danger";
            default:
                return "default";
        }
    }

    render() {
        if (!this.item) return nothing;
        return html`
            <umb-icon name=${UA_AUTOMATION_ICON}></umb-icon>
            <span class="name">${this.item.name}</span>
            <div class="extra">
                <uui-tag color=${this.#statusColor(this.item.status)} look="secondary">${this.item.status}</uui-tag>
                ${when(
                    this.item.health === "Disabled",
                    () => html`<uui-tag color="danger" look="secondary">Disabled</uui-tag>`,
                )}
            </div>
        `;
    }

    static styles = [
        css`
            :host {
                border-radius: var(--uui-border-radius);
                outline-offset: -3px;
                padding: var(--uui-size-space-3) var(--uui-size-space-5);

                display: flex;
                gap: var(--uui-size-space-3);
                align-items: center;

                width: 100%;
            }

            .name {
                flex: 1;
            }

            .extra {
                display: flex;
                gap: var(--uui-size-space-2);
                align-items: center;
            }
        `,
    ];
}

export default UaAutomationSearchResultItemElement;

export { UaAutomationSearchResultItemElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-search-result-item": UaAutomationSearchResultItemElement;
    }
}
