import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import RunNodeShell, { RunMetaRow } from "./RunNodeShell.js";
import type { TriggerNodeData } from "../../types.js";
import { formatTime, type RunNodeStatus } from "./run-node-utils.js";

interface RunTriggerNodeData extends TriggerNodeData {
    runStatus: RunNodeStatus;
    startedUtc?: string | null;
}

function RunTriggerNode({ data }: NodeProps) {
    const nodeData = data as RunTriggerNodeData;
    const startedAt = formatTime(nodeData.startedUtc);

    return (
        <>
            <RunNodeShell
                variant="trigger"
                icon={nodeData.icon}
                label={nodeData.label}
                status={nodeData.runStatus}
                eyebrow="Start"
            >
                {startedAt && <RunMetaRow label="Started">{startedAt}</RunMetaRow>}
            </RunNodeShell>
            <Handle type="source" position={Position.Bottom} isConnectable={false} />
        </>
    );
}

export default memo(RunTriggerNode);
