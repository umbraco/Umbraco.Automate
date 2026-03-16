import { UaAutomationDetailRepository } from "../../../repository/detail/automation-detail.repository.js";
import { UA_WORKSPACE_TREE_ALIAS } from "../../../../workspace-management/constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../../../workspace-management/constants.js";
import { UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../../../constants.js";
import type { UaAutomationMoveModalData } from "./index.js";
import { html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { UmbTreeElement, UmbTreeItemModelBase } from "@umbraco-cms/backoffice/tree";
import { UmbRequestReloadChildrenOfEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import type { UmbEntityUnique } from "@umbraco-cms/backoffice/entity";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService, WorkspacesService } from "../../../../api/sdk.gen.js";

@customElement("ua-automation-move-modal")
export class UaAutomationMoveModalElement extends UmbModalBaseElement<UaAutomationMoveModalData> {
    #detailRepository = new UaAutomationDetailRepository(this);
    #workspaceIds = new Set<string>();

    @state()
    private _automationName?: string;

    @state()
    private _oldParentEntityType?: string;

    @state()
    private _oldParentUnique?: UmbEntityUnique;

    @state()
    private _canSubmit = false;

    async connectedCallback() {
        super.connectedCallback();

        // Load workspace IDs so we can distinguish workspaces from groups in selection
        const { data: workspaces } = await tryExecute(
            this,
            WorkspacesService.getWorkspaces({ query: { skip: 0, take: 100 } }),
        );
        if (workspaces) {
            for (const ws of workspaces.items) {
                this.#workspaceIds.add(ws.id);
            }
        }

        if (!this.data?.unique) return;
        const { data } = await this.#detailRepository.requestByUnique(this.data.unique);
        if (data) {
            this._automationName = data.name;
            if (data.groupId) {
                this._oldParentEntityType = UA_AUTOMATION_GROUP_ENTITY_TYPE;
                this._oldParentUnique = data.groupId;
            } else if (data.workspaceId) {
                this._oldParentEntityType = UA_WORKSPACE_ENTITY_TYPE;
                this._oldParentUnique = data.workspaceId;
            }
        }
    }

    #onSelectionChange() {
        const tree = this.shadowRoot?.getElementById("MoveTarget") as UmbTreeElement;
        this._canSubmit = tree?.getSelection().length > 0;
    }

    async #onSubmit(event: SubmitEvent) {
        event.preventDefault();
        if (!this.data?.unique) return;

        const tree = this.shadowRoot?.getElementById("MoveTarget") as UmbTreeElement;
        const selection = tree?.getSelection();
        if (!selection?.length) return;

        const targetUnique = selection[0];
        if (!targetUnique) return;

        const isWorkspace = this.#workspaceIds.has(targetUnique);

        // Fetch current automation data
        const { data: automation } = await this.#detailRepository.requestByUnique(this.data.unique);
        if (!automation) return;

        if (isWorkspace) {
            automation.groupId = null;
            automation.workspaceId = targetUnique;
        } else {
            // Moving to a group — resolve its workspace to handle cross-workspace moves
            const { data: group } = await tryExecute(
                this,
                AutomationsService.getAutomationsGroupsByGroupId({ path: { groupId: targetUnique } }),
            );
            automation.groupId = targetUnique;
            if (group) {
                automation.workspaceId = group.workspaceId;
            }
        }

        const { error } = await this.#detailRepository.save(automation);

        if (!error) {
            const targetEntityType = isWorkspace ? UA_WORKSPACE_ENTITY_TYPE : UA_AUTOMATION_GROUP_ENTITY_TYPE;

            // Reload old parent
            if (this._oldParentEntityType && this._oldParentUnique !== undefined) {
                await this.#reloadParent(this._oldParentEntityType, this._oldParentUnique);
            }
            // Reload new parent
            await this.#reloadParent(targetEntityType, targetUnique);
            this._submitModal();
        } else {
            this._rejectModal();
        }
    }

    async #reloadParent(entityType: string, unique: UmbEntityUnique) {
        const eventContext = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
        eventContext?.dispatchEvent(
            new UmbRequestReloadChildrenOfEntityEvent({ entityType, unique }),
        );
    }

    render() {
        return html`
            <umb-body-layout headline="Move Automation">
                <uui-box>
                    <p>Select the destination for <strong>${this._automationName ?? "this automation"}</strong>.</p>
                    <uui-form>
                        <form id="MoveForm" @submit="${this.#onSubmit}">
                            <uui-form-layout-item>
                                <umb-tree
                                    id="MoveTarget"
                                    alias="${UA_WORKSPACE_TREE_ALIAS}"
                                    .props=${{
                                        hideTreeRoot: false,
                                        selectionConfiguration: {
                                            multiple: false,
                                        },
                                        selectableFilter: (item: UmbTreeItemModelBase) =>
                                            item.entityType === UA_WORKSPACE_ENTITY_TYPE ||
                                            item.entityType === UA_AUTOMATION_GROUP_ENTITY_TYPE,
                                    }}
                                    @selection-change=${this.#onSelectionChange}
                                ></umb-tree>
                            </uui-form-layout-item>
                        </form>
                    </uui-form>
                </uui-box>
                <uui-button
                    slot="actions"
                    label=${this.localize.term("general_cancel")}
                    @click="${this._rejectModal}"
                >${this.localize.term("general_cancel")}</uui-button>
                <uui-button
                    form="MoveForm"
                    ?disabled=${!this._canSubmit}
                    slot="actions"
                    type="submit"
                    label="Move"
                    look="primary"
                    color="positive"
                ></uui-button>
            </umb-body-layout>
        `;
    }
}

export default UaAutomationMoveModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-move-modal": UaAutomationMoveModalElement;
    }
}
