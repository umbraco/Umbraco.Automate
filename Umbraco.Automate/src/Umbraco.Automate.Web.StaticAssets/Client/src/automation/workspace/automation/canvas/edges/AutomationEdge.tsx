import { memo } from "react";
import {
    BaseEdge,
    EdgeLabelRenderer,
    getSmoothStepPath,
    useReactFlow,
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
    selected,
    label,
    markerEnd,
}: EdgeProps) {
    const [edgePath, labelX, labelY] = getSmoothStepPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    const { deleteElements } = useReactFlow();

    const onDelete = () => {
        deleteElements({ edges: [{ id }] });
    };

    return (
        <>
            <BaseEdge id={id} path={edgePath} markerEnd={markerEnd} interactionWidth={20} />
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
            {selected && (
                <EdgeLabelRenderer>
                    <button
                        className="ua-edge__delete-btn"
                        style={{
                            position: "absolute",
                            transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
                            pointerEvents: "all",
                        }}
                        onClick={onDelete}
                        title="Delete connection"
                        type="button"
                    >
                        ✕
                    </button>
                </EdgeLabelRenderer>
            )}
        </>
    );
}

export default memo(AutomationEdge);
