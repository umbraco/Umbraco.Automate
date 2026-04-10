import { memo, useCallback, useMemo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";

function SwitchNode({ data, id }: NodeProps) {
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

    const handles = useMemo(() => {
        const cases = nodeData.cases ?? [];
        const allCases = [...cases, "default"];
        const total = allCases.length;
        return allCases.map((caseName, i) => ({
            id: caseName,
            left: `${((i + 1) / (total + 1)) * 100}%`,
        }));
    }, [nodeData.cases]);

    return (
        <div className="ua-node ua-node--switch">
            <Handle type="target" position={Position.Top} />
            <div className="ua-node__header">
                <span className="ua-node__icon">{"\u21C4"}</span>
                <span className="ua-node__type">Switch</span>
                {!nodeData.runStatus && (
                    <button
                        className="ua-node__settings-btn"
                        onClick={onSettingsClick}
                        title="Settings"
                        type="button"
                    >
                        {"\u270F\uFE0F"}
                    </button>
                )}
            </div>
            <div className="ua-node__body">
                <span className="ua-node__label">{nodeData.label}</span>
                <span className="ua-node__alias">{nodeData.actionAlias}</span>
            </div>
            {handles.map((handle) => (
                <Handle
                    key={handle.id}
                    type="source"
                    position={Position.Bottom}
                    id={handle.id}
                    style={{ left: handle.left }}
                />
            ))}
            <div className="ua-node__handle-labels">
                {handles.map((handle) => (
                    <span key={handle.id} className="ua-node__handle-label">
                        {handle.id}
                    </span>
                ))}
            </div>
        </div>
    );
}

export default memo(SwitchNode);
