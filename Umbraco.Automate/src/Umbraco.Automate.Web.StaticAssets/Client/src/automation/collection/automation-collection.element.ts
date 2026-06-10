import { html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbCollectionDefaultElement } from "@umbraco-cms/backoffice/collection";

@customElement("ua-automation-collection")
export class UaAutomationCollectionElement extends UmbCollectionDefaultElement {
    protected override renderToolbar() {
        return html`
            <umb-collection-toolbar slot="header">
                <umb-collection-filter-field></umb-collection-filter-field>
            </umb-collection-toolbar>
        `;
    }
}

export { UaAutomationCollectionElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-collection": UaAutomationCollectionElement;
    }
}
