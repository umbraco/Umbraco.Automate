import { UmbDefaultTreeContext } from "@umbraco-cms/backoffice/tree";
import type { UaConnectionTreeItemModel, UaConnectionTreeRootModel } from "./types.js";

export class UaConnectionTreeContext extends UmbDefaultTreeContext<
    UaConnectionTreeItemModel,
    UaConnectionTreeRootModel
> {}

export default UaConnectionTreeContext;
