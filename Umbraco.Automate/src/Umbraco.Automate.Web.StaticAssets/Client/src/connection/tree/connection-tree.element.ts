import { customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbDefaultTreeElement } from "@umbraco-cms/backoffice/tree";

@customElement("ua-connection-tree")
export class UaConnectionTreeElement extends UmbDefaultTreeElement {}

export default UaConnectionTreeElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-tree": UaConnectionTreeElement;
    }
}
