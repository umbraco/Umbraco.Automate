import { memo, useCallback } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import type { TriggerNodeData } from "../types.js";

function TriggerNode({ data, id }: NodeProps) {
    const nodeData = data as TriggerNodeData;

    const onSettingsClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            const event = new CustomEvent("ua:node-settings-open", {
                bubbles: true,
                composed: true,
                detail: { nodeId: id, nodeType: "trigger" },
            });
            (e.target as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
        },
        [id],
    );

    return (
        <div className="ua-node ua-node--trigger">
            <div className="ua-node__header">
                <span className="ua-node__icon">&#x26A1;</span>
                <span className="ua-node__type">Trigger</span>
                {!nodeData.runStatus && (
                    <button
                        className="ua-node__settings-btn"
                        onClick={onSettingsClick}
                        title="Settings"
                        type="button"
                    >
                        &#x270F;&#xFE0F;
                    </button>
                )}
            </div>
            <div className="ua-node__body">
                <span className="ua-node__label">{nodeData.label}</span>
                <span className="ua-node__alias">{nodeData.triggerAlias}</span>
            </div>
            <Handle type="source" position={Position.Bottom} />
        </div>
    );
}

export default memo(TriggerNode);
