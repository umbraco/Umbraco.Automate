import { customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbDefaultTreeElement } from "@umbraco-cms/backoffice/tree";

@customElement("ua-automation-tree")
export class UaAutomationTreeElement extends UmbDefaultTreeElement {}

export default UaAutomationTreeElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-tree": UaAutomationTreeElement;
    }
}
