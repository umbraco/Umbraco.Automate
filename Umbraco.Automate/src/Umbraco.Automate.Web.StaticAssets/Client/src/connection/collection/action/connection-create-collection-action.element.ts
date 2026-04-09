import { html, customElement, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UA_CONNECTION_TYPE_PICKER_MODAL } from "../../modals/type-picker/connection-type-picker-modal.token.js";
import { UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN } from "../../workspace/connection/paths.js";

@customElement("ua-connection-create-collection-action")
export class UaConnectionCreateCollectionActionElement extends UmbLitElement {
    async #onCreate() {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_CONNECTION_TYPE_PICKER_MODAL, {});

        try {
            const { typeAlias } = await modal.onSubmit();
            const path = UA_CREATE_CONNECTION_WORKSPACE_PATH_PATTERN.generateAbsolute({
                connectionType: typeAlias,
            });
            history.pushState(null, "", path);
        } catch {
            // Modal was dismissed
        }
    }

    override render() {
        return html`
            <uui-button look="outline" label=${this.localize.term("uaGeneral_create")} @click=${this.#onCreate}>
                ${this.localize.term("uaGeneral_create")}
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
        "ua-connection-create-collection-action": UaConnectionCreateCollectionActionElement;
    }
}

export default UaConnectionCreateCollectionActionElement;
