import { UmbDefaultTreeItemContext } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceTreeItemModel, UaWorkspaceTreeRootModel } from "../types.js";

export class UaWorkspaceTreeItemContext extends UmbDefaultTreeItemContext<
    UaWorkspaceTreeItemModel,
    UaWorkspaceTreeRootModel
> {}

export default UaWorkspaceTreeItemContext;
