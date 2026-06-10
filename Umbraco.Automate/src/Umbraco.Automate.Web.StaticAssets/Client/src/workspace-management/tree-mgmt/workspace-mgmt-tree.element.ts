import { customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbDefaultTreeElement } from "@umbraco-cms/backoffice/tree";

@customElement("ua-workspace-mgmt-tree")
export class UaWorkspaceMgmtTreeElement extends UmbDefaultTreeElement {}

export default UaWorkspaceMgmtTreeElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-mgmt-tree": UaWorkspaceMgmtTreeElement;
    }
}
