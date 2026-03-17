import type { UmbTreeItemModel, UmbTreeRootModel } from "@umbraco-cms/backoffice/tree";
import type { UaConnectionEntityType, UaConnectionRootEntityType } from "../entity.js";

export interface UaConnectionTreeItemModel extends UmbTreeItemModel {
    entityType: UaConnectionEntityType;
}

export interface UaConnectionTreeRootModel extends UmbTreeRootModel {
    entityType: UaConnectionRootEntityType;
}
