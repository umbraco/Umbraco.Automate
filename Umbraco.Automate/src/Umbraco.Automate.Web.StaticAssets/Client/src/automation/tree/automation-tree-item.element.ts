import { classMap, css, customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbDefaultTreeItemElement } from "@umbraco-cms/backoffice/tree";
import type { UaAutomationTreeItemModel } from "./types.js";

/**
 * Tree-item element for automations. Mirrors the CMS document tree item:
 * dims the label and icon when the automation status is Draft (handled here)
 * while pending-changes is surfaced as an entity sign via the Umb.PendingChanges flag.
 */
@customElement("ua-automation-tree-item")
export class UaAutomationTreeItemElement extends UmbDefaultTreeItemElement {
    @property({ type: Boolean, reflect: true, attribute: "draft" })
    protected _isDraft = false;

    public override set item(value: UaAutomationTreeItemModel) {
        super.item = value;
        this._isDraft = value?.status !== "Published";
    }
    public override get item(): UaAutomationTreeItemModel | undefined {
        return super.item as UaAutomationTreeItemModel | undefined;
    }

    override renderLabel() {
        return html`<span id="label" slot="label" class=${classMap({ draft: this._isDraft })}>
            ${this.item?.name ?? ""}
        </span>`;
    }

    static override styles = [
        ...UmbDefaultTreeItemElement.styles,
        css`
            :host([draft]) #label {
                opacity: 0.6;
            }
            :host([draft]) umb-icon {
                opacity: 0.6;
            }
        `,
    ];
}

export default UaAutomationTreeItemElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-tree-item": UaAutomationTreeItemElement;
    }
}
