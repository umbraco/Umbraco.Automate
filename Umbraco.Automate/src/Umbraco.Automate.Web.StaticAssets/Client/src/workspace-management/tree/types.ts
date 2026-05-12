import type { UmbTreeItemModel, UmbTreeRootModel } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceEntityType, UaWorkspaceRootEntityType } from "../entity.js";
import type { UaAutomationEntityType, UaAutomationGroupEntityType } from "../../automation/entity.js";
import type { AutomationStatusModel } from "../../api/types.gen.js";

export interface UaWorkspaceTreeItemModel extends UmbTreeItemModel {
    entityType: UaWorkspaceEntityType | UaAutomationEntityType | UaAutomationGroupEntityType;
    status?: AutomationStatusModel;
    triggerAlias?: string | null;
}

export interface UaWorkspaceTreeRootModel extends UmbTreeRootModel {
    entityType: UaWorkspaceRootEntityType;
}
