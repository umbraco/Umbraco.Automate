import type { NodeTypes } from "@xyflow/react";
import TriggerNode from "./TriggerNode.js";
import ActionNode from "./ActionNode.js";
import IfNode from "./IfNode.js";
import SwitchNode from "./SwitchNode.js";

export const nodeTypes: NodeTypes = {
    trigger: TriggerNode,
    action: ActionNode,
    if: IfNode,
    switch: SwitchNode,
};
