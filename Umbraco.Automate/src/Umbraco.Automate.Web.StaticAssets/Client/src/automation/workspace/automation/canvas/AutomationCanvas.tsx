import { useCallback, useEffect, useRef } from "react";
import {
    ReactFlow,
    Background,
    Controls,
    MiniMap,
    useNodesState,
    useEdgesState,
    addEdge,
    type OnConnect,
    type Viewport,
    type Node,
    type Edge,
    type ReactFlowInstance,
    type ColorMode,
    type Connection,
} from "@xyflow/react";
import { nodeTypes } from "./nodes/node-types.js";
import AutomationEdge from "./edges/AutomationEdge.js";
import type { CanvasChangeDetail, AddNodeRequestDetail } from "./types.js";

const edgeTypes = {
    automation: AutomationEdge,
};

const defaultEdgeOptions = {
    type: "automation",
    animated: true,
};

interface AutomationCanvasProps {
    nodes: Node[];
    edges: Edge[];
    viewport?: Viewport;
    colorMode?: ColorMode;
    readOnly?: boolean;
    onCanvasChange?: (detail: CanvasChangeDetail) => void;
    onAddNodeRequest?: (detail: AddNodeRequestDetail) => void;
    onDeleteRequest?: (nodes: Node[]) => Promise<boolean>;
}

export default function AutomationCanvas({
    nodes: externalNodes,
    edges: externalEdges,
    viewport,
    colorMode = "light",
    readOnly = false,
    onCanvasChange,
    onAddNodeRequest,
    onDeleteRequest,
}: AutomationCanvasProps) {
    const [nodes, setNodes, onNodesChange] = useNodesState(externalNodes);
    const [edges, setEdges, onEdgesChange] = useEdgesState(externalEdges);
    const rfInstance = useRef<ReactFlowInstance | null>(null);

    // Sync external prop changes into internal state.
    // This handles new nodes/edges added from outside React (e.g. modal picker).
    useEffect(() => {
        setNodes(externalNodes);
    }, [externalNodes, setNodes]);

    useEffect(() => {
        setEdges(externalEdges);
    }, [externalEdges, setEdges]);

    const emitChange = useCallback(
        (updatedNodes: Node[], updatedEdges: Edge[]) => {
            if (!onCanvasChange || !rfInstance.current) return;
            const vp = rfInstance.current.getViewport();
            onCanvasChange({ nodes: updatedNodes, edges: updatedEdges, viewport: vp });
        },
        [onCanvasChange],
    );

    const handleNodesChange: typeof onNodesChange = useCallback(
        (changes) => {
            onNodesChange(changes);

            // Only emit for user-initiated changes (drag complete, remove).
            // Ignore React Flow internals like dimensions, select, and fitView positioning.
            const isUserChange = changes.some(
                (c) =>
                    c.type === "remove" ||
                    (c.type === "position" && c.dragging === false && c.position != null),
            );
            if (!isUserChange) return;

            queueMicrotask(() => {
                setNodes((currentNodes) => {
                    setEdges((currentEdges) => {
                        emitChange(currentNodes, currentEdges);
                        return currentEdges;
                    });
                    return currentNodes;
                });
            });
        },
        [onNodesChange, setNodes, setEdges, emitChange],
    );

    const handleEdgesChange: typeof onEdgesChange = useCallback(
        (changes) => {
            onEdgesChange(changes);

            const isUserChange = changes.some((c) => c.type === "remove");
            if (!isUserChange) return;

            queueMicrotask(() => {
                setEdges((currentEdges) => {
                    setNodes((currentNodes) => {
                        emitChange(currentNodes, currentEdges);
                        return currentNodes;
                    });
                    return currentEdges;
                });
            });
        },
        [onEdgesChange, setNodes, setEdges, emitChange],
    );

    // Keep a ref to the latest edges so isValidConnection can read them
    // without useStore (which requires being inside the ReactFlow provider).
    const edgesRef = useRef(edges);
    edgesRef.current = edges;

    // Prevent self-loops and cycles. For branching nodes (If/Switch), each source handle
    // can have one outgoing edge. For regular nodes, only one outgoing edge total.
    const isValidConnection = useCallback(
        (connection: Edge | Connection) => {
            if (connection.source === connection.target) return false;

            // Walk the edge chain from target forward — if we reach source, it's a cycle.
            // Exclude edges that would be replaced by this connection (same source+handle or same target).
            const remaining = edgesRef.current.filter(
                (e) =>
                    !(e.source === connection.source && (e.sourceHandle ?? null) === (connection.sourceHandle ?? null)) &&
                    e.target !== connection.target,
            );
            let current: string | null = connection.target;
            const visited = new Set<string>();
            while (current) {
                if (current === connection.source) return false;
                if (visited.has(current)) break;
                visited.add(current);
                const next = remaining.find((e) => e.source === current);
                current = next?.target ?? null;
            }
            return true;
        },
        [],
    );

    const onConnect: OnConnect = useCallback(
        (params) => {
            setEdges((eds) => {
                // Remove any existing edge from the same source+handle or to the same target.
                // For branching nodes (If/Switch), each handle can have one connection.
                const filtered = eds.filter(
                    (e) =>
                        !(e.source === params.source && (e.sourceHandle ?? null) === (params.sourceHandle ?? null)) &&
                        e.target !== params.target,
                );

                // Auto-label edges from named handles (If: true/false, Switch: case names)
                const label = params.sourceHandle ?? undefined;
                const updated = addEdge({ ...params, type: "automation", animated: true, label }, filtered);
                setNodes((currentNodes) => {
                    emitChange(currentNodes, updated);
                    return currentNodes;
                });
                return updated;
            });
        },
        [setEdges, setNodes, emitChange],
    );

    // Track the source of a connection drag for drop-to-add.
    const connectStartRef = useRef<{ nodeId: string; handleId: string | null } | null>(null);

    const onConnectStart = useCallback((_event: MouseEvent | TouchEvent, params: { nodeId: string | null; handleId: string | null }) => {
        connectStartRef.current = { nodeId: params.nodeId ?? "", handleId: params.handleId };
    }, []);

    const onConnectEnd = useCallback(
        (event: MouseEvent | TouchEvent) => {
            if (!onAddNodeRequest || !rfInstance.current || !connectStartRef.current) return;

            // If the drop landed on a handle, onConnect already handled it.
            const target = (event as MouseEvent).target as HTMLElement | null;
            if (target?.closest(".react-flow__handle")) return;

            const clientX = "changedTouches" in event ? event.changedTouches[0].clientX : (event as MouseEvent).clientX;
            const clientY = "changedTouches" in event ? event.changedTouches[0].clientY : (event as MouseEvent).clientY;

            const position = rfInstance.current.screenToFlowPosition({ x: clientX, y: clientY });
            const { nodeId, handleId } = connectStartRef.current;
            connectStartRef.current = null;

            onAddNodeRequest({
                position,
                connectFrom: { sourceStepId: nodeId, sourceHandle: handleId },
            });
        },
        [onAddNodeRequest],
    );

    const onInit = useCallback((instance: ReactFlowInstance) => {
        rfInstance.current = instance;
    }, []);

    // Persist viewport changes (pan/zoom) — onMoveEnd fires after the user finishes
    // panning or zooming, so the saved viewport survives reload.
    const handleMoveEnd = useCallback(
        (_event: MouseEvent | TouchEvent | null, vp: Viewport) => {
            if (!onCanvasChange) return;
            setNodes((currentNodes) => {
                setEdges((currentEdges) => {
                    onCanvasChange({ nodes: currentNodes, edges: currentEdges, viewport: vp });
                    return currentEdges;
                });
                return currentNodes;
            });
        },
        [onCanvasChange, setNodes, setEdges],
    );

    const handleBeforeDelete = useCallback(
        async ({ nodes: nodesToDelete }: { nodes: Node[]; edges: Edge[] }) => {
            if (nodesToDelete.length === 0 || !onDeleteRequest) return true;
            return onDeleteRequest(nodesToDelete);
        },
        [onDeleteRequest],
    );

    const handlePaneClick = useCallback(
        (event: React.MouseEvent) => {
            if (!onAddNodeRequest || !rfInstance.current) return;
            if (event.detail !== 2) return;
            const bounds = (event.target as HTMLElement).closest(".react-flow")?.getBoundingClientRect();
            if (!bounds) return;
            const position = rfInstance.current.screenToFlowPosition({
                x: event.clientX,
                y: event.clientY,
            });
            onAddNodeRequest({ position });
        },
        [onAddNodeRequest],
    );

    // Route placeholder-node clicks to the workflow view via a custom event so the user
    // doesn't have to hit the inner button precisely (and React Flow's wrapper doesn't
    // swallow the click during drag detection).
    const handleNodeClick = useCallback(
        (event: React.MouseEvent, node: Node) => {
            if (node.type !== "trigger-placeholder") return;
            const ev = new CustomEvent("ua:add-trigger-request", {
                bubbles: true,
                composed: true,
            });
            (event.target as HTMLElement).closest(".react-flow")?.dispatchEvent(ev);
        },
        [],
    );

    return (
        <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={readOnly ? undefined : handleNodesChange}
            onEdgesChange={readOnly ? undefined : handleEdgesChange}
            onConnect={readOnly ? undefined : onConnect}
            onConnectStart={readOnly ? undefined : onConnectStart}
            onConnectEnd={readOnly ? undefined : onConnectEnd}
            isValidConnection={readOnly ? undefined : isValidConnection}
            onBeforeDelete={readOnly ? undefined : handleBeforeDelete}
            onInit={onInit}
            onMoveEnd={handleMoveEnd}
            onPaneClick={readOnly ? undefined : handlePaneClick}
            onNodeClick={readOnly ? undefined : handleNodeClick}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            defaultEdgeOptions={defaultEdgeOptions}
            defaultViewport={viewport}
            fitView={!viewport}
            fitViewOptions={{ padding: 0.3, maxZoom: 0.85 }}
            colorMode={colorMode}
            deleteKeyCode={readOnly ? null : ["Backspace", "Delete"]}
            nodesConnectable={!readOnly}
            nodesDraggable={!readOnly}
            elementsSelectable
            proOptions={{ hideAttribution: true }}
        >
            <Background />
            <Controls />
            <MiniMap
                nodeColor={(node) => {
                    switch (node.type) {
                        case "trigger": return "#6366f1";
                        case "if": return "#f59e0b";
                        case "switch": return "#8b5cf6";
                        case "approval": return "#0ea5e9";
                        default: return "#3b82f6";
                    }
                }}
                zoomable
                pannable
            />
        </ReactFlow>
    );
}
