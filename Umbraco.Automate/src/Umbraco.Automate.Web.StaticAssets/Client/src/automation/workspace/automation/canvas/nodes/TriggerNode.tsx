import { memo, useCallback } from "react";
import { Handle, Position, useReactFlow, type NodeProps } from "@xyflow/react";
import type { TriggerNodeData } from "../types.js";
import AddActionButton from "./AddActionButton.js";

function TriggerNode({ data, id }: NodeProps) {
    const nodeData = data as TriggerNodeData;
    const { deleteElements } = useReactFlow();

    const dispatchSettingsOpen = useCallback(
        (target: HTMLElement) => {
            const event = new CustomEvent("ua:node-settings-open", {
                bubbles: true,
                composed: true,
                detail: { nodeId: id, nodeType: "trigger" },
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

    const hasSettings = nodeData.hasSettings ?? true;

    const onDoubleClick = useCallback(
        (e: React.MouseEvent) => {
            if (!hasSettings) return;
            e.stopPropagation();
            dispatchSettingsOpen(e.currentTarget as HTMLElement);
        },
        [hasSettings, dispatchSettingsOpen],
    );

    const onDeleteClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            deleteElements({ nodes: [{ id }] });
        },
        [id, deleteElements],
    );

    return (
        <div className="ua-node ua-node--trigger" onDoubleClick={onDoubleClick}>
            <div className="ua-node__header">
                {nodeData.icon && (
                    <span className="ua-node__icon">
                        <uui-icon name={nodeData.icon}></uui-icon>
                    </span>
                )}
                <div className="ua-node__title">
                    <uui-tag className="ua-node__kind-tag" look="secondary">Trigger</uui-tag>
                    <span className="ua-node__type">{nodeData.label}</span>
                </div>
                {!nodeData.runStatus && (
                    <div className="ua-node__actions ua-action-bar">
                        {hasSettings && (
                            <button
                                className="ua-action-bar__btn"
                                onClick={onSettingsClick}
                                title="Settings"
                                type="button"
                            >
                                <uui-icon name="icon-edit"></uui-icon>
                            </button>
                        )}
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
            <Handle type="source" position={Position.Bottom} />
            {!nodeData.runStatus && <AddActionButton nodeId={id} />}
        </div>
    );
}

export default memo(TriggerNode);
