import type { NodeTypes } from "@xyflow/react";
import TriggerNode from "./TriggerNode.js";
import ActionNode from "./ActionNode.js";

export const nodeTypes: NodeTypes = {
    trigger: TriggerNode,
    action: ActionNode,
};
