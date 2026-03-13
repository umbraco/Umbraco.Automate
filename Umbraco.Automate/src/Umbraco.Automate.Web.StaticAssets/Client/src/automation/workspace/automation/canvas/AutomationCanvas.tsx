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
    onCanvasChange?: (detail: CanvasChangeDetail) => void;
    onAddNodeRequest?: (detail: AddNodeRequestDetail) => void;
}

export default function AutomationCanvas({
    nodes: externalNodes,
    edges: externalEdges,
    viewport,
    colorMode = "light",
    onCanvasChange,
    onAddNodeRequest,
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

    const onConnect: OnConnect = useCallback(
        (params) => {
            setEdges((eds) => {
                const updated = addEdge({ ...params, type: "automation", animated: true }, eds);
                setNodes((currentNodes) => {
                    emitChange(currentNodes, updated);
                    return currentNodes;
                });
                return updated;
            });
        },
        [setEdges, setNodes, emitChange],
    );

    const onInit = useCallback((instance: ReactFlowInstance) => {
        rfInstance.current = instance;
    }, []);

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

    return (
        <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={handleNodesChange}
            onEdgesChange={handleEdgesChange}
            onConnect={onConnect}
            onInit={onInit}
            onPaneClick={handlePaneClick}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            defaultEdgeOptions={defaultEdgeOptions}
            defaultViewport={viewport}
            fitView={!viewport}
            colorMode={colorMode}
            deleteKeyCode={["Backspace", "Delete"]}
            proOptions={{ hideAttribution: true }}
        >
            <Background />
            <Controls />
            <MiniMap
                nodeColor={(node) =>
                    node.type === "trigger" ? "#6366f1" : "#3b82f6"
                }
                zoomable
                pannable
            />
        </ReactFlow>
    );
}
