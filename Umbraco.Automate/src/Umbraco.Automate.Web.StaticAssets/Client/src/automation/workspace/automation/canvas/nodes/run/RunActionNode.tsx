import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import RunNodeShell, { RunMetaRow } from "./RunNodeShell.js";
import type { ActionNodeData } from "../../types.js";
import type { UaStepRunModel } from "../../../../../../run/types.js";
import { formatDuration, totalDurationMs, type RunNodeStatus } from "./run-node-utils.js";

interface RunActionNodeData extends ActionNodeData {
    runStatus: RunNodeStatus;
    /** All iterations of this step within the run (>1 inside a forEach / while body). */
    stepRuns: UaStepRunModel[];
    /** ForEach: binding expression resolving to the collection (e.g. "${trigger.items}"). */
    collectionBinding?: string;
    /** ForEach: realised body-iteration count at runtime. */
    bodyIterations?: number;
}

function RunActionNode({ data }: NodeProps) {
    const nodeData = data as RunActionNodeData;
    const stepRuns = nodeData.stepRuns ?? [];
    const iterations = stepRuns.length;
    const duration = formatDuration(totalDurationMs(stepRuns));
    const totalRetries = stepRuns.reduce((acc, sr) => acc + sr.retryCount, 0);
    const firstError = stepRuns.find((sr) => sr.error)?.error;
    const isContainer = nodeData.bodyIterations !== undefined;

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
                {nodeData.collectionBinding && (
                    <RunMetaRow label="Items">
                        <code className="ua-run-node__binding" title={nodeData.collectionBinding}>
                            {nodeData.collectionBinding}
                        </code>
                    </RunMetaRow>
                )}
                {isContainer && (
                    <RunMetaRow label="Iterations">{nodeData.bodyIterations}</RunMetaRow>
                )}
                {!isContainer && iterations > 1 && (
                    <RunMetaRow label="Iterations">{iterations}</RunMetaRow>
                )}
                {duration && (
                    <RunMetaRow label={iterations > 1 ? "Total time" : "Duration"}>
                        {duration}
                    </RunMetaRow>
                )}
                {totalRetries > 0 && <RunMetaRow label="Retries">{totalRetries}</RunMetaRow>}
                {firstError && (
                    <RunMetaRow label="Error">
                        <span className="ua-run-node__error" title={firstError}>{firstError}</span>
                    </RunMetaRow>
                )}
            </RunNodeShell>
            <Handle type="source" position={Position.Bottom} isConnectable={false} />
        </>
    );
}

export default memo(RunActionNode);
