import { customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbTreeItemElementBase } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceTreeItemModel } from "../types.js";
import type { UaWorkspaceTreeItemContext } from "./workspace-tree-item.context.js";

@customElement("ua-workspace-tree-item")
export class UaWorkspaceTreeItemElement extends UmbTreeItemElementBase<
    UaWorkspaceTreeItemModel,
    UaWorkspaceTreeItemContext
> {
    @state()
    private _name = "";

    @state()
    private _icon = "icon-users";

    public override set api(value: UaWorkspaceTreeItemContext | undefined) {
        if (value) {
            this.observe(
                value.treeItem,
                (item) => {
                    if (item) {
                        this._name = item.name;
                        this._icon = item.icon ?? "icon-users";
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

export default UaWorkspaceTreeItemElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-tree-item": UaWorkspaceTreeItemElement;
    }
}
