import { UmbDefaultTreeItemContext } from "@umbraco-cms/backoffice/tree";
import type { UaConnectionTreeItemModel, UaConnectionTreeRootModel } from "../types.js";

export class UaConnectionTreeItemContext extends UmbDefaultTreeItemContext<
    UaConnectionTreeItemModel,
    UaConnectionTreeRootModel
> {}

export default UaConnectionTreeItemContext;
