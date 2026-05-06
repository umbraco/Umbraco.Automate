import { memo, useMemo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import RunNodeShell, { RunMetaRow } from "./RunNodeShell.js";
import type { ActionNodeData } from "../../types.js";
import type { UaStepRunModel } from "../../../../../../run/types.js";
import { formatDuration, totalDurationMs, type RunNodeStatus, type RunBranchState } from "./run-node-utils.js";

interface RunSwitchNodeData extends ActionNodeData {
    runStatus: RunNodeStatus;
    stepRuns: UaStepRunModel[];
    branches?: Record<string, RunBranchState>;
}

function RunSwitchNode({ data }: NodeProps) {
    const nodeData = data as RunSwitchNodeData;
    const stepRuns = nodeData.stepRuns ?? [];
    const iterations = stepRuns.length;
    const duration = formatDuration(totalDurationMs(stepRuns));
    const branches = nodeData.branches ?? {};

    const handles = useMemo(() => {
        const cases = nodeData.cases ?? [];
        const allCases = [...cases, "default"];
        const total = allCases.length;
        return allCases.map((caseName, i) => ({
            id: caseName,
            left: `${((i + 1) / (total + 1)) * 100}%`,
        }));
    }, [nodeData.cases]);

    return (
        <>
            <Handle type="target" position={Position.Top} isConnectable={false} />
            <RunNodeShell
                variant="switch"
                icon={nodeData.icon}
                label={nodeData.label}
                status={nodeData.runStatus}
                subtitle={nodeData.stepAlias || undefined}
            >
                {iterations > 1 && <RunMetaRow label="Iterations">{iterations}</RunMetaRow>}
                {handles.map((handle) => {
                    const state = branches[handle.id];
                    const taken = !!state?.taken;
                    return (
                        <RunMetaRow key={handle.id} label={handle.id}>
                            <span
                                className={`ua-run-node__branch-pill ua-run-node__branch-pill--${taken ? "taken" : "skipped"}`}
                            >
                                {taken ? "Taken" : "Not taken"}
                            </span>
                        </RunMetaRow>
                    );
                })}
                {duration && (
                    <RunMetaRow label={iterations > 1 ? "Total time" : "Duration"}>
                        {duration}
                    </RunMetaRow>
                )}
            </RunNodeShell>
            {handles.map((handle) => (
                <Handle
                    key={handle.id}
                    type="source"
                    position={Position.Bottom}
                    id={handle.id}
                    style={{ left: handle.left }}
                    isConnectable={false}
                />
            ))}
        </>
    );
}

export default memo(RunSwitchNode);
