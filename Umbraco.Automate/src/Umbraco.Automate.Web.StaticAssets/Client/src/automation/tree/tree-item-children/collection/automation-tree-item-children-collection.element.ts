import { html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbCollectionDefaultElement } from "@umbraco-cms/backoffice/collection";

@customElement("ua-automation-tree-item-children-collection")
export class UaAutomationTreeItemChildrenCollectionElement extends UmbCollectionDefaultElement {
    protected override renderToolbar() {
        return html`
            <umb-collection-toolbar slot="header">
                <umb-collection-filter-field></umb-collection-filter-field>
            </umb-collection-toolbar>
        `;
    }
}

export { UaAutomationTreeItemChildrenCollectionElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-tree-item-children-collection": UaAutomationTreeItemChildrenCollectionElement;
    }
}
