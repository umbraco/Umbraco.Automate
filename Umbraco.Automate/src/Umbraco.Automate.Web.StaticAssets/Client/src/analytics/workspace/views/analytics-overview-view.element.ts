import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

@customElement("ua-analytics-overview-view")
export class UaAnalyticsOverviewViewElement extends UmbLitElement {
    override render() {
        return html``;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
            }
        `,
    ];
}

export default UaAnalyticsOverviewViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-overview-view": UaAnalyticsOverviewViewElement;
    }
}
