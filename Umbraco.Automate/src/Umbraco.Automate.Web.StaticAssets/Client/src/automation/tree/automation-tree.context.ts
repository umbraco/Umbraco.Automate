import { UmbDefaultTreeContext } from "@umbraco-cms/backoffice/tree";
import type { UaAutomationTreeItemModel, UaAutomationTreeRootModel } from "./types.js";

export class UaAutomationTreeContext extends UmbDefaultTreeContext<
    UaAutomationTreeItemModel,
    UaAutomationTreeRootModel
> {}

export default UaAutomationTreeContext;
