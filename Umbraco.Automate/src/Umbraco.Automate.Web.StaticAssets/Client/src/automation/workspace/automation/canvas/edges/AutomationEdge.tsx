import { memo, useCallback } from "react";
import {
    BaseEdge,
    EdgeLabelRenderer,
    getSmoothStepPath,
    useReactFlow,
    type EdgeProps,
} from "@xyflow/react";
import type { EdgeFilterData } from "../types.js";

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
    data,
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

    const edgeData = data as EdgeFilterData | undefined;
    const hasFilter = edgeData?.filter && edgeData.filter.groups.length > 0;

    const onFilterClick = useCallback(() => {
        const detail = { edgeId: id, filter: edgeData?.filter ?? null };
        document.dispatchEvent(new CustomEvent("ua:edge-filter-open", { detail }));
    }, [id, edgeData?.filter]);

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
            <EdgeLabelRenderer>
                <div
                    className="ua-edge__actions"
                    style={{
                        position: "absolute",
                        transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY + (label ? 16 : 0)}px)`,
                        pointerEvents: "all",
                    }}
                >
                    {hasFilter && (
                        <button
                            className="ua-edge__filter-badge"
                            onClick={onFilterClick}
                            title="Edit filter"
                            type="button"
                        >
                            ⚡
                        </button>
                    )}
                    {selected && !hasFilter && (
                        <button
                            className="ua-edge__filter-badge ua-edge__filter-badge--add"
                            onClick={onFilterClick}
                            title="Add filter"
                            type="button"
                        >
                            +⚡
                        </button>
                    )}
                    {selected && (
                        <button
                            className="ua-edge__delete-btn"
                            onClick={onDelete}
                            title="Delete connection"
                            type="button"
                        >
                            ✕
                        </button>
                    )}
                </div>
            </EdgeLabelRenderer>
        </>
    );
}

export default memo(AutomationEdge);
