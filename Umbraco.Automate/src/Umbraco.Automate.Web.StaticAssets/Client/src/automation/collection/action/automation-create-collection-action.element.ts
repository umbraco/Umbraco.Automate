import { html, customElement, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../../workspace/automation/paths.js";

@customElement("ua-automation-create-collection-action")
export class UaAutomationCreateCollectionActionElement extends UmbLitElement {
    #onCreate() {
        const path = UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({});
        history.pushState(null, "", path);
    }

    override render() {
        return html`
            <uui-button look="outline" label="Create" @click=${this.#onCreate}>
                Create
            </uui-button>
        `;
    }

    static override styles = [
        css`
            :host {
                display: flex;
                align-items: center;
            }
        `,
    ];
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-create-collection-action": UaAutomationCreateCollectionActionElement;
    }
}

export default UaAutomationCreateCollectionActionElement;
