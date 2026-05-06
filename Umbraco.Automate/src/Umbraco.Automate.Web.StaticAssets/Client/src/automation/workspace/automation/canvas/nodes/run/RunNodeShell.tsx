import type { ReactNode } from "react";
import type { RunNodeStatus } from "./run-node-utils.js";

interface RunNodeShellProps {
    variant: "trigger" | "action" | "if" | "switch";
    icon?: string;
    label: string;
    status: RunNodeStatus;
    /** Floating uppercase pill above the card (used for the trigger to mimic the "Start" label). */
    eyebrow?: string;
    /** Small monospace tag rendered below the title — e.g. step alias. */
    subtitle?: string;
    children?: ReactNode;
}

export default function RunNodeShell({
    variant,
    icon,
    label,
    status,
    eyebrow,
    subtitle,
    children,
}: RunNodeShellProps) {
    return (
        <div className={`ua-run-node ua-run-node--${variant} run-status-${status.toLowerCase()}`}>
            {eyebrow && <div className="ua-run-node__eyebrow">{eyebrow}</div>}
            <div className="ua-run-node__header">
                {icon && (
                    <span className="ua-run-node__icon">
                        <uui-icon name={icon}></uui-icon>
                    </span>
                )}
                <div className="ua-run-node__title-block">
                    <span className="ua-run-node__title" title={label}>{label}</span>
                    {subtitle && <code className="ua-run-node__subtitle">{subtitle}</code>}
                </div>
                <span
                    className={`ua-run-node__status-dot ua-run-node__status-dot--${status.toLowerCase()}`}
                    title={status}
                    aria-label={status}
                ></span>
            </div>
            {children && <div className="ua-run-node__body">{children}</div>}
        </div>
    );
}

interface RunMetaRowProps {
    label: string;
    children: ReactNode;
}

export function RunMetaRow({ label, children }: RunMetaRowProps) {
    return (
        <div className="ua-run-node__row">
            <span className="ua-run-node__row-key">{label}</span>
            <span className="ua-run-node__row-val">{children}</span>
        </div>
    );
}
