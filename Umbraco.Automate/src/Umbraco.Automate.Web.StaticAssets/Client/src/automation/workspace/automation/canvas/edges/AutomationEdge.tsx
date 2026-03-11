import { memo } from "react";
import {
    BaseEdge,
    getSmoothStepPath,
    type EdgeProps,
} from "@xyflow/react";

function AutomationEdge({
    id,
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    label,
    markerEnd,
}: EdgeProps) {
    const [edgePath] = getSmoothStepPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    return (
        <>
            <BaseEdge id={id} path={edgePath} markerEnd={markerEnd} />
            {label && (
                <text>
                    <textPath
                        href={`#${id}`}
                        startOffset="50%"
                        textAnchor="middle"
                        dominantBaseline="central"
                        className="ua-edge__label"
                    >
                        {label}
                    </textPath>
                </text>
            )}
        </>
    );
}

export default memo(AutomationEdge);
