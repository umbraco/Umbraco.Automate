import type { UmbTreeItemModel, UmbTreeRootModel } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceEntityType, UaWorkspaceRootEntityType } from "../entity.js";

export interface UaWorkspaceTreeItemModel extends UmbTreeItemModel {
    entityType: UaWorkspaceEntityType;
}

export interface UaWorkspaceTreeRootModel extends UmbTreeRootModel {
    entityType: UaWorkspaceRootEntityType;
}
