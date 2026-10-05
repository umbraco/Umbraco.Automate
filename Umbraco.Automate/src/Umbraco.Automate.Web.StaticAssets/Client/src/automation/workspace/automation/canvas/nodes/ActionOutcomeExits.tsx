import { memo, useEffect, useMemo } from "react";
import { Handle, Position, useStore, useUpdateNodeInternals } from "@xyflow/react";
import type { ActionNodeData } from "../types.js";
import { useLocalizeString } from "../localize-context.js";
import { buildOutcomeExits, parseConnectedExits } from "../utils/outcome-exits.js";
import AddActionButton from "./AddActionButton.js";

interface ActionOutcomeExitsProps {
    nodeId: string;
    nodeData: ActionNodeData;
}

const NO_OUTCOMES: NonNullable<ActionNodeData["outcomes"]> = [];

/**
 * The right-edge exits of an action that declares outcomes. Reads which lines currently leave the
 * node straight from the React Flow store; the selector returns a string so the node only
 * re-renders when its own set of connected handles changes, not on every edge or viewport update.
 */
function ActionOutcomeExits({ nodeId, nodeData }: ActionOutcomeExitsProps) {
    const localize = useLocalizeString();
    const updateNodeInternals = useUpdateNodeInternals();

    const connectedKey = useStore((s) =>
        JSON.stringify(
            s.edges
                .filter((e) => e.source === nodeId)
                .map((e) => e.sourceHandle ?? "")
                .sort(),
        ),
    );

    const { exits, hasBothPathsConflict } = useMemo(
        () =>
            buildOutcomeExits(
                nodeData.actionAlias,
                nodeData.outcomes ?? NO_OUTCOMES,
                nodeData.outcomesUnknown ?? false,
                parseConnectedExits(JSON.parse(connectedKey) as string[]),
            ),
        [nodeData.actionAlias, nodeData.outcomes, nodeData.outcomesUnknown, connectedKey],
    );

    // Handles are added and removed as lines and outcomes change; tell React Flow to re-measure
    // them so existing edges re-attach to the right rows.
    const handleSignature = exits.map((e) => e.handleId).join("\u0000");
    useEffect(() => {
        updateNodeInternals(nodeId);
    }, [nodeId, handleSignature, updateNodeInternals]);

    const showAdd = !nodeData.runStatus;

    return (
        <>
            {hasBothPathsConflict && (
                <div className="ua-node__warning" role="alert">
                    <uui-icon name="icon-alert"></uui-icon>
                    <span>{localize("#uaOutcomeExits_bothPathsWarning")}</span>
                </div>
            )}
            <div className="ua-node__switch-cases">
                {exits.map((exit) => {
                    const text =
                        exit.kind === "missing"
                            ? `${localize("#uaOutcomeExits_missing")}: ${exit.label}`
                            : localize(exit.label);
                    const label = exit.isDefault ? `${text} ${localize("#uaOutcomeExits_default")}` : text;
                    const rowClass =
                        exit.kind === "missing"
                            ? "ua-node__switch-case ua-node__switch-case--missing"
                            : "ua-node__switch-case";
                    return (
                        <div key={exit.handleId} className={rowClass}>
                            <span
                                className="ua-node__switch-case-label"
                                title={exit.tooltip ? localize(exit.tooltip) : label}
                            >
                                {label}
                            </span>
                            <Handle type="source" position={Position.Right} id={exit.handleId} />
                            {showAdd && (exit.kind === "declared") && (
                                <AddActionButton
                                    nodeId={nodeId}
                                    sourceHandle={exit.handleId}
                                    className="ua-node__add-action--right"
                                />
                            )}
                        </div>
                    );
                })}
            </div>
        </>
    );
}

export default memo(ActionOutcomeExits);
