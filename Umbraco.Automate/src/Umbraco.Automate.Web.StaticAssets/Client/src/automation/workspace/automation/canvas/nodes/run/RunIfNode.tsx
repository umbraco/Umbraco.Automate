import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import RunNodeShell, { RunMetaRow } from "./RunNodeShell.js";
import type { ActionNodeData } from "../../types.js";
import type { UaStepRunModel } from "../../../../../../run/types.js";
import { formatDuration, type RunNodeStatus, type RunBranchState } from "./run-node-utils.js";

interface RunIfNodeData extends ActionNodeData {
    runStatus: RunNodeStatus;
    stepRun?: UaStepRunModel;
    branches?: { true?: RunBranchState; false?: RunBranchState };
}

function RunIfNode({ data }: NodeProps) {
    const nodeData = data as RunIfNodeData;
    const duration = formatDuration(nodeData.stepRun?.durationMs);
    const branches = nodeData.branches ?? {};

    return (
        <>
            <Handle type="target" position={Position.Top} isConnectable={false} />
            <RunNodeShell
                variant="if"
                icon={nodeData.icon}
                label={nodeData.label}
                status={nodeData.runStatus}
                subtitle={nodeData.stepAlias || undefined}
            >
                <RunMetaRow label="True">
                    <BranchPill state={branches.true} variant="true" />
                </RunMetaRow>
                <RunMetaRow label="False">
                    <BranchPill state={branches.false} variant="false" />
                </RunMetaRow>
                {duration && <RunMetaRow label="Duration">{duration}</RunMetaRow>}
            </RunNodeShell>
            <Handle type="source" position={Position.Bottom} id="true" style={{ left: "30%" }} isConnectable={false} />
            <Handle type="source" position={Position.Bottom} id="false" style={{ left: "70%" }} isConnectable={false} />
        </>
    );
}

function BranchPill({ state, variant }: { state?: RunBranchState; variant: "true" | "false" }) {
    if (!state || !state.taken) {
        return <span className="ua-run-node__branch-pill ua-run-node__branch-pill--skipped">Not taken</span>;
    }
    return (
        <span className={`ua-run-node__branch-pill ua-run-node__branch-pill--${variant}`}>
            Taken
        </span>
    );
}

export default memo(RunIfNode);
