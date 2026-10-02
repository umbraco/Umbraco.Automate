/** A `uui-tag` / `uui-button` colour. */
export type UaStatusColor = "default" | "positive" | "warning" | "danger";

/**
 * The tag colour for a run or step status, shared so every screen agrees on it. Accepts both run
 * statuses (`AutomationRunStatusModel`) and step statuses (`StepRunStatusModel`).
 *
 * - `warning` covers anything in flight or paused (a run waiting on an approval is `Suspended`, its
 *   step `WaitingForInput`), and `Rejected`: a refusal is not an error, so never `danger`, but it is
 *   the outcome someone scanning a list wants to spot, so it does not blend in as `default` either.
 * - `default` is for statuses that ended without an outcome worth highlighting (`Skipped`, `Cancelled`).
 */
export function getRunStatusColor(status: string): UaStatusColor {
    switch (status) {
        case "Completed":
            return "positive";
        case "Running":
        case "Pending":
        case "Suspended":
        case "WaitingForInput":
        case "Sleeping":
        case "Rejected":
            return "warning";
        case "Failed":
            return "danger";
        default:
            return "default";
    }
}
