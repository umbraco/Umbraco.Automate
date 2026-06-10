import { customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbDefaultTreeElement } from "@umbraco-cms/backoffice/tree";

@customElement("ua-workspace-tree")
export class UaWorkspaceTreeElement extends UmbDefaultTreeElement {}

export default UaWorkspaceTreeElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-tree": UaWorkspaceTreeElement;
    }
}
