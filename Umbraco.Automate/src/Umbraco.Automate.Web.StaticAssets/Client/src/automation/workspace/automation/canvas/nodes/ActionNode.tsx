import { memo, useCallback } from "react";
import { Handle, Position, useReactFlow, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";
import AddActionButton from "./AddActionButton.js";

function ActionNode({ data, id }: NodeProps) {
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
        <div className="ua-node ua-node--action" onDoubleClick={onDoubleClick}>
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
            <Handle type="source" position={Position.Bottom} />
            {!nodeData.runStatus && <AddActionButton nodeId={id} />}
        </div>
    );
}

export default memo(ActionNode);
