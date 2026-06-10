import { html, customElement, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_ENTITY_CONTEXT } from "@umbraco-cms/backoffice/entity";
import { UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../../workspace/automation/paths.js";

@customElement("ua-automation-create-collection-action")
export class UaAutomationCreateCollectionActionElement extends UmbLitElement {
    #entityContext?: typeof UMB_ENTITY_CONTEXT.TYPE;

    constructor() {
        super();
        this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
            this.#entityContext = context;
        });
    }

    #onCreate() {
        const entityType = this.#entityContext?.getEntityType() ?? "ua:automation-root";
        const unique = this.#entityContext?.getUnique() ?? "null";
        const path = UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({
            parentEntityType: entityType,
            parentUnique: unique ?? "null",
        });
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
