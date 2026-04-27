import type { NodeTypes } from "@xyflow/react";
import TriggerNode from "./TriggerNode.js";
import TriggerPlaceholderNode from "./TriggerPlaceholderNode.js";
import ActionNode from "./ActionNode.js";
import IfNode from "./IfNode.js";
import SwitchNode from "./SwitchNode.js";

export const nodeTypes: NodeTypes = {
    trigger: TriggerNode,
    "trigger-placeholder": TriggerPlaceholderNode,
    action: ActionNode,
    if: IfNode,
    switch: SwitchNode,
};
