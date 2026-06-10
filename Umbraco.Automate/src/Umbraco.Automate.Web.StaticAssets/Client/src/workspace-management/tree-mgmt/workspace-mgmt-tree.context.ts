import { UmbDefaultTreeContext } from "@umbraco-cms/backoffice/tree";
import type { UaWorkspaceMgmtTreeItemModel, UaWorkspaceMgmtTreeRootModel } from "./types.js";

export class UaWorkspaceMgmtTreeContext extends UmbDefaultTreeContext<
    UaWorkspaceMgmtTreeItemModel,
    UaWorkspaceMgmtTreeRootModel
> {}

export default UaWorkspaceMgmtTreeContext;
