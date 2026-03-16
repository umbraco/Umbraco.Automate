import type { UmbTreeItemModel, UmbTreeRootModel } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceMgmtEntityType, UaWorkspaceMgmtRootEntityType } from "../entity.js";

export interface UaWorkspaceMgmtTreeItemModel extends UmbTreeItemModel {
    entityType: UaWorkspaceMgmtEntityType;
}

export interface UaWorkspaceMgmtTreeRootModel extends UmbTreeRootModel {
    entityType: UaWorkspaceMgmtRootEntityType;
}
