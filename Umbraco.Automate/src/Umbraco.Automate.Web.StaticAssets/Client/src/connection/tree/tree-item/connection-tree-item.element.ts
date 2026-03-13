import { customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbTreeItemElementBase } from "@umbraco-cms/backoffice/tree";
import type { UaConnectionTreeItemModel } from "../types.js";
import type { UaConnectionTreeItemContext } from "./connection-tree-item.context.js";

@customElement("ua-connection-tree-item")
export class UaConnectionTreeItemElement extends UmbTreeItemElementBase<
    UaConnectionTreeItemModel,
    UaConnectionTreeItemContext
> {
    @state()
    private _name = "";

    @state()
    private _icon = "icon-plug";

    public override set api(value: UaConnectionTreeItemContext | undefined) {
        if (value) {
            this.observe(
                value.treeItem,
                (item) => {
                    if (item) {
                        this._name = item.name;
                        this._icon = item.icon ?? "icon-plug";
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

export default UaConnectionTreeItemElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-tree-item": UaConnectionTreeItemElement;
    }
}
