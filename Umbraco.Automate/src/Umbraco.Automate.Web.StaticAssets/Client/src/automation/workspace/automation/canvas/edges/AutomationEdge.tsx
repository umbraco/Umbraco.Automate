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
    source,
    target,
    sourceHandleId,
    targetHandleId,
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

    const onInsertClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            const detail = {
                position: {
                    x: (sourceX + targetX) / 2 - 100,
                    y: (sourceY + targetY) / 2,
                },
                insertBetween: {
                    sourceStepId: source,
                    sourceHandle: sourceHandleId ?? null,
                    targetStepId: target,
                    targetHandle: targetHandleId ?? null,
                },
            };
            const event = new CustomEvent("ua:add-node-request", {
                detail,
                bubbles: true,
                composed: true,
            });
            (e.currentTarget as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
        },
        [source, target, sourceHandleId, targetHandleId, sourceX, sourceY, targetX, targetY],
    );

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
                    <button
                        className="ua-edge__btn"
                        onClick={onInsertClick}
                        title="Insert action"
                        type="button"
                    >
                        <uui-icon name="icon-add"></uui-icon>
                    </button>
                    <button
                        className={`ua-edge__btn${hasFilter ? " ua-edge__btn--active" : ""}`}
                        onClick={onFilterClick}
                        title={hasFilter ? "Edit filter" : "Add filter"}
                        type="button"
                    >
                        <uui-icon name="icon-filter"></uui-icon>
                    </button>
                    <button
                        className="ua-edge__btn ua-edge__btn--danger"
                        onClick={onDelete}
                        title="Delete connection"
                        type="button"
                    >
                        <uui-icon name="icon-trash"></uui-icon>
                    </button>
                </div>
            </EdgeLabelRenderer>
        </>
    );
}

export default memo(AutomationEdge);
