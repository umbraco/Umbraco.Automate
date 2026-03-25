import { memo, useCallback } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";

function ActionNode({ data, id }: NodeProps) {
    const nodeData = data as ActionNodeData;

    const onSettingsClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            const event = new CustomEvent("ua:node-settings-open", {
                bubbles: true,
                composed: true,
                detail: { nodeId: id, nodeType: "action" },
            });
            (e.target as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
        },
        [id],
    );

    return (
        <div className="ua-node ua-node--action">
            <Handle type="target" position={Position.Top} />
            <div className="ua-node__header">
                <span className="ua-node__icon">⚙️</span>
                <span className="ua-node__type">Action</span>
                {!nodeData.runStatus && (
                    <button
                        className="ua-node__settings-btn"
                        onClick={onSettingsClick}
                        title="Settings"
                        type="button"
                    >
                        ✏️
                    </button>
                )}
            </div>
            <div className="ua-node__body">
                <span className="ua-node__label">{nodeData.label}</span>
                <span className="ua-node__alias">{nodeData.actionAlias}</span>
            </div>
            <Handle type="source" position={Position.Bottom} />
        </div>
    );
}

export default memo(ActionNode);
