import { UmbDefaultTreeContext } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceTreeItemModel, UaWorkspaceTreeRootModel } from "./types.js";

export class UaWorkspaceTreeContext extends UmbDefaultTreeContext<
    UaWorkspaceTreeItemModel,
    UaWorkspaceTreeRootModel
> {}

export default UaWorkspaceTreeContext;
