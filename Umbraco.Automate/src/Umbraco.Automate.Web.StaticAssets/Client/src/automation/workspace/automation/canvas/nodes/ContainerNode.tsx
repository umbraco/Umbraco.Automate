import { memo, useCallback } from "react";
import { Handle, Position, useReactFlow, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";
import AddActionButton from "./AddActionButton.js";
import { BODY_HANDLE, DONE_HANDLE } from "../utils/model-to-flow.js";

/**
 * Canvas node for the container control flow steps — While, For Each and Parallel.
 *
 * These own a body of steps that repeats (While, For Each) or fans out (Parallel), so unlike a
 * plain action they need two outputs: the body, and whatever runs once the container is finished.
 * Before this node existed they rendered a single unnamed handle, and "what runs after the loop"
 * had to be inferred from the shape of the graph — a straight chain drawn after a While was folded
 * into the loop body instead.
 */
function ContainerNode({ data, id }: NodeProps) {
    const nodeData = data as ActionNodeData;
    const { deleteElements } = useReactFlow();

    const dispatchSettingsOpen = useCallback(
        (target: HTMLElement) => {
            const event = new CustomEvent("ua:node-settings-open", {
                bubbles: true,
                composed: true,
                detail: { nodeId: id, nodeType: "action" },
            });
            target.closest(".react-flow")?.dispatchEvent(event);
        },
        [id],
    );

    const onSettingsClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            dispatchSettingsOpen(e.target as HTMLElement);
        },
        [dispatchSettingsOpen],
    );

    const onDoubleClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            dispatchSettingsOpen(e.currentTarget as HTMLElement);
        },
        [dispatchSettingsOpen],
    );

    const onDeleteClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            deleteElements({ nodes: [{ id }] });
        },
        [id, deleteElements],
    );

    return (
        <div className="ua-node ua-node--container" onDoubleClick={onDoubleClick}>
            <Handle type="target" position={Position.Top} />
            <div className="ua-node__header">
                {nodeData.icon && (
                    <span className="ua-node__icon">
                        <uui-icon name={nodeData.icon}></uui-icon>
                    </span>
                )}
                <span className="ua-node__type">{nodeData.label}</span>
                {!nodeData.runStatus && (
                    <div className="ua-node__actions ua-action-bar">
                        <button
                            className="ua-action-bar__btn"
                            onClick={onSettingsClick}
                            title="Settings"
                            type="button"
                        >
                            <uui-icon name="icon-edit"></uui-icon>
                        </button>
                        <button
                            className="ua-action-bar__btn ua-action-bar__btn--danger"
                            onClick={onDeleteClick}
                            title="Delete"
                            type="button"
                        >
                            <uui-icon name="icon-trash"></uui-icon>
                        </button>
                    </div>
                )}
            </div>
            <div className="ua-node__body">
                <div className="ua-node__chips">
                    {nodeData.stepAlias && (
                        <code className="ua-node__chip" title="Step alias">
                            {nodeData.stepAlias}
                        </code>
                    )}
                    <code className="ua-node__chip ua-node__chip--id" title="Step ID">
                        {id}
                    </code>
                </div>
            </div>
            <Handle type="source" position={Position.Bottom} id={BODY_HANDLE} style={{ left: "30%" }} />
            <Handle type="source" position={Position.Bottom} id={DONE_HANDLE} style={{ left: "70%" }} />
            <div className="ua-node__handle-labels">
                <span className="ua-node__handle-label">Body</span>
                <span className="ua-node__handle-label">Done</span>
            </div>
            {!nodeData.runStatus && (
                <>
                    <AddActionButton nodeId={id} sourceHandle={BODY_HANDLE} style={{ left: "30%" }} />
                    <AddActionButton nodeId={id} sourceHandle={DONE_HANDLE} style={{ left: "70%" }} />
                </>
            )}
        </div>
    );
}

export default memo(ContainerNode);
