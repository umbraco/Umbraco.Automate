import { html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbCollectionDefaultElement } from "@umbraco-cms/backoffice/collection";

@customElement("ua-connection-collection")
export class UaConnectionCollectionElement extends UmbCollectionDefaultElement {
    protected override renderToolbar() {
        return html`
            <umb-collection-toolbar slot="header">
                <umb-collection-filter-field></umb-collection-filter-field>
            </umb-collection-toolbar>
        `;
    }
}

export { UaConnectionCollectionElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-collection": UaConnectionCollectionElement;
    }
}
