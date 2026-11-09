import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

@customElement("ua-analytics-workspace")
export class UaAnalyticsWorkspaceElement extends UmbLitElement {
    override render() {
        return html`<umb-workspace-editor
            headline=${this.localize.term("uaMenu_analytics")}
            .enforceNoFooter=${true}></umb-workspace-editor>`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                width: 100%;
                height: 100%;
            }
        `,
    ];
}

export default UaAnalyticsWorkspaceElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-workspace": UaAnalyticsWorkspaceElement;
    }
}
