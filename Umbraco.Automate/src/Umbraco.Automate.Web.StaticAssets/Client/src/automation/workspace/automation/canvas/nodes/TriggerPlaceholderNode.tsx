import { memo, useCallback } from "react";
import type { NodeProps } from "@xyflow/react";

function TriggerPlaceholderNode(_props: NodeProps) {
    const onClick = useCallback((e: React.MouseEvent) => {
        e.stopPropagation();
        const event = new CustomEvent("ua:add-trigger-request", {
            bubbles: true,
            composed: true,
        });
        (e.currentTarget as HTMLElement).closest(".react-flow")?.dispatchEvent(event);
    }, []);

    return (
        <button type="button" className="ua-node ua-node--placeholder" onClick={onClick}>
            <span className="ua-node__icon">
                <uui-icon name="icon-add"></uui-icon>
            </span>
            <span className="ua-node__placeholder-label">Add trigger</span>
        </button>
    );
}

export default memo(TriggerPlaceholderNode);
