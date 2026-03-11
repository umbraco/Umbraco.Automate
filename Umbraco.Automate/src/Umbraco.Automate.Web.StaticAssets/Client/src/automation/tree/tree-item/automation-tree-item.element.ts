import { customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbTreeItemElementBase } from "@umbraco-cms/backoffice/tree";
import type { UaAutomationTreeItemModel } from "../types.js";
import type { UaAutomationTreeItemContext } from "./automation-tree-item.context.js";

@customElement("ua-automation-tree-item")
export class UaAutomationTreeItemElement extends UmbTreeItemElementBase<
    UaAutomationTreeItemModel,
    UaAutomationTreeItemContext
> {
    @state()
    private _name = "";

    @state()
    private _icon = "icon-mindmap";

    public override set api(value: UaAutomationTreeItemContext | undefined) {
        if (value) {
            this.observe(
                value.treeItem,
                (item) => {
                    if (item) {
                        this._name = item.name;
                        this._icon = item.icon ?? "icon-mindmap";
                    }
                },
                "_treeItem",
            );
        }
        super.api = value;
    }

    override renderLabel() {
        return html`<span>${this._name}</span>`;
    }

    protected override _getIconName(): string {
        return this._icon;
    }
}

export default UaAutomationTreeItemElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-tree-item": UaAutomationTreeItemElement;
    }
}
