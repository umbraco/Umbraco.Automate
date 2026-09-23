import { useCallback } from "react";
import { Position, useReactFlow, type Node, type XYPosition } from "@xyflow/react";

interface AddActionButtonProps {
    nodeId: string;
    /** Identifier of the source handle this button is attached to (e.g. "true", "false", a switch case name). Null for nodes with a single output. */
    sourceHandle?: string | null;
    /** Absolute positioning override applied to the wrapper. */
    style?: React.CSSProperties;
    /** Extra class names appended to the button (e.g. a positioning modifier). */
    className?: string;
}

const NEW_NODE_OFFSET_Y = 180;
const NEW_NODE_GAP = 40;
/** Fallbacks for nodes React Flow has not measured yet (within the node min/max width in canvas.styles.css). */
const DEFAULT_NODE_WIDTH = 240;
const DEFAULT_NODE_HEIGHT = 100;
const MAX_PLACEMENT_ATTEMPTS = 50;

interface Rect {
    x: number;
    y: number;
    width: number;
    height: number;
}

function nodeRect(node: Node): Rect {
    return {
        x: node.position.x,
        y: node.position.y,
        width: node.measured?.width ?? node.width ?? DEFAULT_NODE_WIDTH,
        height: node.measured?.height ?? node.height ?? DEFAULT_NODE_HEIGHT,
    };
}

function overlaps(a: Rect, b: Rect): boolean {
    return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

export default function AddActionButton({ nodeId, sourceHandle, style, className }: AddActionButtonProps) {
    const { getNode, getNodes, getInternalNode } = useReactFlow();

    /**
     * Places the new node next to the handle that was clicked, so each output of a multi-output
     * node (Approval, If, container body/done, Switch cases) gets its own spot, then nudges it
     * away from the handle until it no longer overlaps an existing node. Without this, a second
     * outcome or a repeated Parallel branch lands exactly on top of the previous step.
     */
    const computeNewNodePosition = useCallback(
        (node: Node): XYPosition => {
            const source = nodeRect(node);
            const handle = getInternalNode(nodeId)?.internals.handleBounds?.source?.find(
                (h) => (h.id ?? null) === (sourceHandle ?? null),
            );

            const size = { width: source.width, height: DEFAULT_NODE_HEIGHT };
            let candidate: XYPosition;
            let step: XYPosition;

            if (handle?.position === Position.Right) {
                // Side handles (Switch cases): place to the right, level with the handle; fan downward.
                const handleCenterY = handle.y + handle.height / 2;
                candidate = {
                    x: source.x + source.width + NEW_NODE_GAP * 2,
                    y: source.y + handleCenterY - size.height / 2,
                };
                step = { x: 0, y: size.height + NEW_NODE_GAP };
            } else {
                // Bottom handles: an off-centre handle (e.g. left 30% / right 70%) pushes the node
                // half a node-width to that side, and further repeats fan out in the same direction.
                const handleFraction = handle ? (handle.x + handle.width / 2) / source.width : 0.5;
                const direction = handleFraction < 0.45 ? -1 : handleFraction > 0.55 ? 1 : 0;
                candidate = {
                    x: source.x + (direction * (source.width + NEW_NODE_GAP)) / 2,
                    y: source.y + NEW_NODE_OFFSET_Y,
                };
                step = { x: (direction || 1) * (size.width + NEW_NODE_GAP), y: 0 };
            }

            const obstacles = getNodes()
                .filter((n) => n.id !== nodeId)
                .map(nodeRect);
            const padded = (p: XYPosition): Rect => ({
                x: p.x - NEW_NODE_GAP / 2,
                y: p.y - NEW_NODE_GAP / 2,
                width: size.width + NEW_NODE_GAP,
                height: size.height + NEW_NODE_GAP,
            });

            for (let i = 0; i < MAX_PLACEMENT_ATTEMPTS; i++) {
                const rect = padded(candidate);
                if (!obstacles.some((o) => overlaps(rect, o))) break;
                candidate = { x: candidate.x + step.x, y: candidate.y + step.y };
            }

            return candidate;
        },
        [nodeId, sourceHandle, getNodes, getInternalNode],
    );

    const onClick = useCallback(
        (e: React.MouseEvent) => {
            e.stopPropagation();
            const node = getNode(nodeId);
            if (!node) return;
            const event = new CustomEvent("ua:add-node-request", {
                bubbles: true,
                composed: true,
                detail: {
                    position: computeNewNodePosition(node),
                    connectFrom: { sourceStepId: nodeId, sourceHandle: sourceHandle ?? null },
                },
            });
            (e.currentTarget as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
        },
        [nodeId, sourceHandle, getNode, computeNewNodePosition],
    );

    return (
        <button
            type="button"
            className={className ? `ua-node__add-action ${className}` : "ua-node__add-action"}
            style={style}
            onClick={onClick}
            title={sourceHandle ? `Add action — ${sourceHandle}` : "Add action"}
        >
            <uui-icon name="icon-add"></uui-icon>
        </button>
    );
}
