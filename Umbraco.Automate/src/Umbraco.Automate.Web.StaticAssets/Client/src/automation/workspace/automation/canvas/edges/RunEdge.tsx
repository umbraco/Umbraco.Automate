import { memo } from "react";
import { BaseEdge, getBezierPath, type EdgeProps } from "@xyflow/react";

export interface RunEdgeData {
    /** Whether this branch executed at runtime. Skipped branches are dimmed. */
    taken?: boolean;
    /** Optional override label (e.g. "true"/"false"/case name) — defaults to props.label. */
    [key: string]: unknown;
}

function RunEdge({
    id,
    sourceHandleId,
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    label,
    data,
    markerEnd,
}: EdgeProps) {
    const [edgePath, labelX, labelY] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    const edgeData = data as RunEdgeData | undefined;
    const taken = edgeData?.taken ?? true;
    const handleClass = sourceHandleId ? ` ua-run-edge--${cssSafe(sourceHandleId)}` : "";
    const stateClass = taken ? " ua-run-edge--taken" : " ua-run-edge--skipped";

    return (
        <>
            <BaseEdge
                id={id}
                path={edgePath}
                markerEnd={markerEnd}
                className={`ua-run-edge${handleClass}${stateClass}`}
                interactionWidth={0}
            />
            {label && (
                <text
                    x={labelX}
                    y={labelY - 6}
                    textAnchor="middle"
                    dominantBaseline="central"
                    className="ua-run-edge__label"
                >
                    {label}
                </text>
            )}
        </>
    );
}

function cssSafe(s: string): string {
    return s.replace(/[^a-z0-9_-]/gi, "-").toLowerCase();
}

export default memo(RunEdge);
