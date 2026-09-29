/**
 * Collision avoidance for newly added canvas nodes, shared by the node "+" button (which places
 * against React Flow's measured nodes) and the workspace view's edge-insert path (which places
 * against the saved step positions).
 */

export interface Point {
    x: number;
    y: number;
}

export interface Rect extends Point {
    width: number;
    height: number;
}

/** Fallbacks for nodes React Flow has not measured yet (within the node min/max width in canvas.styles.css). */
export const DEFAULT_NODE_WIDTH = 240;
export const DEFAULT_NODE_HEIGHT = 100;
export const NEW_NODE_GAP = 40;
const MAX_PLACEMENT_ATTEMPTS = 50;

export function overlaps(a: Rect, b: Rect): boolean {
    return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

/**
 * Nudges `candidate` by `step` until a node of `size` placed there (padded by half a gap on every
 * side) no longer overlaps any of `obstacles`. Gives up after a bounded number of attempts and
 * returns the last candidate, so a crowded canvas still gets a node rather than an endless loop.
 */
export function findFreePosition(
    candidate: Point,
    size: { width: number; height: number },
    step: Point,
    obstacles: Rect[],
): Point {
    let current = candidate;
    for (let i = 0; i < MAX_PLACEMENT_ATTEMPTS; i++) {
        const padded: Rect = {
            x: current.x - NEW_NODE_GAP / 2,
            y: current.y - NEW_NODE_GAP / 2,
            width: size.width + NEW_NODE_GAP,
            height: size.height + NEW_NODE_GAP,
        };
        if (!obstacles.some((o) => overlaps(padded, o))) return current;
        current = { x: current.x + step.x, y: current.y + step.y };
    }
    return current;
}
