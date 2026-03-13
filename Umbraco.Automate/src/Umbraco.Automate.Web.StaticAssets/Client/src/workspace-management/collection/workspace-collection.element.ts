import { html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbCollectionDefaultElement } from "@umbraco-cms/backoffice/collection";

@customElement("ua-workspace-collection")
export class UaWorkspaceCollectionElement extends UmbCollectionDefaultElement {
    protected override renderToolbar() {
        return html`
            <umb-collection-toolbar slot="header">
                <umb-collection-filter-field></umb-collection-filter-field>
            </umb-collection-toolbar>
        `;
    }
}

export { UaWorkspaceCollectionElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-collection": UaWorkspaceCollectionElement;
    }
}
