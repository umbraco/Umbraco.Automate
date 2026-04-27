import { useCallback } from "react";
import { useReactFlow } from "@xyflow/react";

interface AddActionButtonProps {
    nodeId: string;
    /** Identifier of the source handle this button is attached to (e.g. "true", "false", a switch case name). Null for nodes with a single output. */
    sourceHandle?: string | null;
    /** Absolute positioning override applied to the wrapper. */
    style?: React.CSSProperties;
}

const NEW_NODE_OFFSET_Y = 180;

export default function AddActionButton({ nodeId, sourceHandle, style }: AddActionButtonProps) {
    const { getNode } = useReactFlow();

    const onClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            const node = getNode(nodeId);
            if (!node) return;
            const event = new CustomEvent("ua:add-node-request", {
                bubbles: true,
                composed: true,
                detail: {
                    position: { x: node.position.x, y: node.position.y + NEW_NODE_OFFSET_Y },
                    connectFrom: { sourceStepId: nodeId, sourceHandle: sourceHandle ?? null },
                },
            });
            (e.currentTarget as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
        },
        [nodeId, sourceHandle, getNode],
    );

    return (
        <button
            type="button"
            className="ua-node__add-action"
            style={style}
            onClick={onClick}
            title={sourceHandle ? `Add action — ${sourceHandle}` : "Add action"}
        >
            <uui-icon name="icon-add"></uui-icon>
        </button>
    );
}
