import { memo, useCallback, useMemo } from "react";
import { Handle, Position, useReactFlow, type NodeProps } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";
import AddActionButton from "./AddActionButton.js";

function SwitchNode({ data, id }: NodeProps) {
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

    // Outcomes stack vertically (cases first, then the default branch) so the node stays
    // a fixed, legible width regardless of case count — it grows downward instead of
    // sideways. Each outcome gets its own source handle on the right edge.
    const outcomes = useMemo(
        () => [...(nodeData.cases ?? []), "default"],
        [nodeData.cases],
    );

    return (
        <div className="ua-node ua-node--switch" onDoubleClick={onDoubleClick}>
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
                {nodeData.stepAlias && (
                    <code className="ua-node__step-alias" title={`Step alias — id: ${id}`}>
                        {nodeData.stepAlias}
                    </code>
                )}
            </div>
            <div className="ua-node__switch-cases">
                {outcomes.map((outcome) => (
                    <div key={outcome} className="ua-node__switch-case">
                        <span className="ua-node__switch-case-label" title={outcome}>
                            {outcome}
                        </span>
                        <Handle type="source" position={Position.Right} id={outcome} />
                        {!nodeData.runStatus && (
                            <AddActionButton
                                nodeId={id}
                                sourceHandle={outcome}
                                className="ua-node__add-action--right"
                            />
                        )}
                    </div>
                ))}
            </div>
        </div>
    );
}

export default memo(SwitchNode);
