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
            {!nodeData.runStatus && handles.map((handle) => (
                <AddActionButton
                    key={`add-${handle.id}`}
                    nodeId={id}
                    sourceHandle={handle.id}
                    style={{ left: handle.left }}
                />
            ))}
        </div>
    );
}

export default memo(SwitchNode);
