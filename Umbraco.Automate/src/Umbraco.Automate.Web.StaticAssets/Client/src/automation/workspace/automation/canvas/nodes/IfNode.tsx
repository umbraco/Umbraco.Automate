import { memo, useCallback } from "react";
import { Handle, Position, useReactFlow, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";
import AddActionButton from "./AddActionButton.js";

function IfNode({ data, id }: NodeProps) {
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
        <div className="ua-node ua-node--if" onDoubleClick={onDoubleClick}>
            <Handle type="target" position={Position.Top} />
            <div className="ua-node__header">
                {nodeData.icon && (
                    <span className="ua-node__icon">
                        <uui-icon name={nodeData.icon}></uui-icon>
                    </span>
                )}
                <span className="ua-node__type">{nodeData.label}</span>
                {!nodeData.runStatus && (
                    <>
                        <button
                            className="ua-node__settings-btn"
                            onClick={onSettingsClick}
                            title="Settings"
                            type="button"
                        >
                            <uui-icon name="icon-edit"></uui-icon>
                        </button>
                        <button
                            className="ua-node__delete-btn"
                            onClick={onDeleteClick}
                            title="Delete"
                            type="button"
                        >
                            <uui-icon name="icon-trash"></uui-icon>
                        </button>
                    </>
                )}
            </div>
            <div className="ua-node__body">
                {nodeData.stepAlias && (
                    <code className="ua-node__step-alias" title={`Step alias — id: ${id}`}>
                        {nodeData.stepAlias}
                    </code>
                )}
            </div>
            <Handle type="source" position={Position.Bottom} id="true" style={{ left: '30%' }} />
            <Handle type="source" position={Position.Bottom} id="false" style={{ left: '70%' }} />
            <div className="ua-node__handle-labels">
                <span className="ua-node__handle-label">True</span>
                <span className="ua-node__handle-label">False</span>
            </div>
            {!nodeData.runStatus && (
                <>
                    <AddActionButton nodeId={id} sourceHandle="true" style={{ left: "30%" }} />
                    <AddActionButton nodeId={id} sourceHandle="false" style={{ left: "70%" }} />
                </>
            )}
        </div>
    );
}

export default memo(IfNode);
