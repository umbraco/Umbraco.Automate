import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import RunNodeShell, { RunMetaRow } from "./RunNodeShell.js";
import type { ActionNodeData } from "../../types.js";
import type { UaStepRunModel } from "../../../../../../run/types.js";
import { formatDuration, type RunNodeStatus } from "./run-node-utils.js";

interface RunActionNodeData extends ActionNodeData {
    runStatus: RunNodeStatus;
    stepRun?: UaStepRunModel;
}

function RunActionNode({ data }: NodeProps) {
    const nodeData = data as RunActionNodeData;
    const duration = formatDuration(nodeData.stepRun?.durationMs);
    const error = nodeData.stepRun?.error;
    const retries = nodeData.stepRun?.retryCount ?? 0;

    return (
        <>
            <Handle type="target" position={Position.Top} isConnectable={false} />
            <RunNodeShell
                variant="action"
                icon={nodeData.icon}
                label={nodeData.label}
                status={nodeData.runStatus}
                subtitle={nodeData.stepAlias || undefined}
            >
                {duration && <RunMetaRow label="Duration">{duration}</RunMetaRow>}
                {retries > 0 && <RunMetaRow label="Retries">{retries}</RunMetaRow>}
                {error && (
                    <RunMetaRow label="Error">
                        <span className="ua-run-node__error" title={error}>{error}</span>
                    </RunMetaRow>
                )}
            </RunNodeShell>
            <Handle type="source" position={Position.Bottom} isConnectable={false} />
        </>
    );
}

export default memo(RunActionNode);
