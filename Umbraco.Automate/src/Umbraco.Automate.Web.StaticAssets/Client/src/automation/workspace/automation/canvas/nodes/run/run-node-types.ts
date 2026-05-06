import type { NodeTypes } from "@xyflow/react";
import RunTriggerNode from "./RunTriggerNode.js";
import RunActionNode from "./RunActionNode.js";
import RunIfNode from "./RunIfNode.js";
import RunSwitchNode from "./RunSwitchNode.js";

export const runNodeTypes: NodeTypes = {
    trigger: RunTriggerNode,
    action: RunActionNode,
    if: RunIfNode,
    switch: RunSwitchNode,
};
