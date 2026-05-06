import type { StepRunStatusModel } from "../../../../../../api/types.gen.js";
import type { UaStepRunModel } from "../../../../../../run/types.js";

export type RunNodeStatus = StepRunStatusModel | "Pending";

export function formatDuration(ms: number | null | undefined): string | null {
    if (ms == null) return null;
    if (ms < 1000) return `${Math.round(ms)}ms`;
    const seconds = ms / 1000;
    if (seconds < 60) return `${seconds.toFixed(seconds < 10 ? 2 : 1)}s`;
    const minutes = Math.floor(seconds / 60);
    const remaining = Math.round(seconds % 60);
    return `${minutes}m ${remaining}s`;
}

export function formatTime(iso: string | null | undefined): string | null {
    if (!iso) return null;
    const date = new Date(iso);
    if (isNaN(date.getTime())) return null;
    return date.toLocaleString(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
    });
}

export interface RunBranchState {
    /** Whether at least one downstream step run took this branch. */
    taken: boolean;
    /** Worst-case status across the branch (Failed > Running > Completed > Skipped > Pending). */
    aggregateStatus: RunNodeStatus;
}

/**
 * Walks downstream from a (sourceNodeId, sourceHandle) edge to determine whether the
 * branch was taken at runtime, by checking whether any reachable step has a non-Pending
 * stepRun.
 */
export function computeBranchState(
    sourceNodeId: string,
    sourceHandle: string | null | undefined,
    edges: Array<{ source: string; target: string; sourceHandle?: string | null }>,
    stepRunsByStepId: Map<string, UaStepRunModel>,
): RunBranchState {
    const visited = new Set<string>();
    const queue: string[] = [];

    for (const edge of edges) {
        if (edge.source !== sourceNodeId) continue;
        if ((edge.sourceHandle ?? null) !== (sourceHandle ?? null)) continue;
        queue.push(edge.target);
    }

    let worst: RunNodeStatus = "Pending";

    while (queue.length > 0) {
        const id = queue.shift()!;
        if (visited.has(id)) continue;
        visited.add(id);

        const run = stepRunsByStepId.get(id);
        if (run) worst = worsen(worst, run.status);

        for (const edge of edges) {
            if (edge.source === id) queue.push(edge.target);
        }
    }

    return {
        taken: worst !== "Pending" && worst !== "Skipped",
        aggregateStatus: worst,
    };
}

const STATUS_RANK: Record<RunNodeStatus, number> = {
    Pending: 0,
    Skipped: 1,
    Sleeping: 2,
    WaitingForInput: 3,
    Completed: 4,
    Running: 5,
    Failed: 6,
};

function worsen(a: RunNodeStatus, b: RunNodeStatus): RunNodeStatus {
    return STATUS_RANK[b] > STATUS_RANK[a] ? b : a;
}
