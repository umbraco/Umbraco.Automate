import type { UmbTreeItemModel, UmbTreeRootModel } from "@umbraco-cms/backoffice/tree";
import type { UaAutomationEntityType, UaAutomationRootEntityType } from "../entity.js";
import type { AutomationStatusModel } from "../../api/types.gen.js";

export interface UaAutomationTreeItemModel extends UmbTreeItemModel {
    entityType: UaAutomationEntityType;
    status: AutomationStatusModel;
    isEnabled: boolean;
}

export interface UaAutomationTreeRootModel extends UmbTreeRootModel {
    entityType: UaAutomationRootEntityType;
}
