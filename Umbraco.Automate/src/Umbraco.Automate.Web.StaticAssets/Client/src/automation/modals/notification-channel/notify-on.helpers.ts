import type { NotifyOnFlag } from "../../../api/types.gen.js";

/**
 * Atomic notification flags shown as toggles in the channel modal, in render order.
 * Composites (e.g. `FailedOrSuspended`) are NOT in this list — multi-select makes them
 * unnecessary — but the parser below still recognises them to preserve legacy values.
 */
export const NOTIFY_ON_FLAGS: ReadonlyArray<NotifyOnFlag> = [
    "Failed",
    "Suspended",
    "Resumed",
    "Completed",
    "Recovered",
    "Warning",
    "Disabled",
    "ReEnabled",
];

const LEGACY_COMPOSITES: Readonly<Record<string, ReadonlyArray<NotifyOnFlag>>> = {
    FailedOrSuspended: ["Failed", "Suspended"],
};

/**
 * Parses a wire-format notify-on value into the set of atomic flags it represents.
 * Accepts the named composite `"FailedOrSuspended"` (legacy data), `"Never"`/empty (no flags),
 * and comma-separated combinations like `"Failed, Disabled"`.
 */
export function parseNotifyOn(value: string | null | undefined): Set<NotifyOnFlag> {
    const result = new Set<NotifyOnFlag>();
    if (!value) {
        return result;
    }

    for (const raw of value.split(",")) {
        const token = raw.trim();
        if (!token || token === "Never") {
            continue;
        }

        const composite = LEGACY_COMPOSITES[token];
        if (composite) {
            composite.forEach((flag) => result.add(flag));
            continue;
        }

        if (NOTIFY_ON_FLAGS.includes(token as NotifyOnFlag)) {
            result.add(token as NotifyOnFlag);
        }
    }

    return result;
}

/**
 * Serialises a set of atomic flags into the wire format. Returns `"Never"` when no flags are
 * selected (the named zero value the [Flags] enum understands).
 */
export function serializeNotifyOn(flags: ReadonlySet<NotifyOnFlag>): string {
    if (flags.size === 0) {
        return "Never";
    }
    return NOTIFY_ON_FLAGS.filter((f) => flags.has(f)).join(", ");
}
