import { UmbDefaultTreeItemContext } from "@umbraco-cms/backoffice/tree";
import type { UaAutomationTreeItemModel, UaAutomationTreeRootModel } from "../types.js";

export class UaAutomationTreeItemContext extends UmbDefaultTreeItemContext<
    UaAutomationTreeItemModel,
    UaAutomationTreeRootModel
> {}

export default UaAutomationTreeItemContext;
