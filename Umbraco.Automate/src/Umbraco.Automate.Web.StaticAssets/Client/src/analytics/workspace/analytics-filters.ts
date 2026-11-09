import {
    uauiCompareFor,
    uauiPresetRange,
    type UauiCompare,
    type UauiDatePreset,
    type UauiDateRange,
    type UauiDateSpan,
} from "@umbraco/analytics-ui";

/** The id of the custom preset that sits between the built-in "Last 30 days" and "Custom". */
export const UA_ANALYTICS_PRESET_LAST_90_DAYS = "last-90-days";

/** What the Analytics page is filtered by. Every view reads these through `UaAnalyticsWorkspaceContext`. */
export interface UaAnalyticsFilters {
    range: UauiDateRange;
    /** Undefined when the comparison is turned off. */
    compare?: UauiCompare;
    /** Undefined means all workspaces the user can access. */
    workspaceId?: string;
    /** The browser's IANA time zone; the server folds runs into days in this zone. */
    timeZone: string;
}

/** The presets `uaui-date-range` offers, in order. "Custom" is added last by the element itself. */
export function createAnalyticsPresets(last90DaysLabel: string): Array<UauiDatePreset> {
    return [
        { id: "last-7-days" },
        { id: "last-30-days" },
        {
            id: UA_ANALYTICS_PRESET_LAST_90_DAYS,
            label: last90DaysLabel,
            range: (today) => resolvePresetRange(UA_ANALYTICS_PRESET_LAST_90_DAYS, today)!,
        },
    ];
}

/** The browser's current day as an ISO date, in local time. */
export function getTodayIso(now: Date = new Date()): string {
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${now.getFullYear()}-${month}-${day}`;
}

export function getBrowserTimeZone(): string {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
}

/** The dates of a preset as of `today`, or undefined for "custom" and unknown ids. */
export function resolvePresetRange(id: string, today: string): UauiDateSpan | undefined {
    if (id === UA_ANALYTICS_PRESET_LAST_90_DAYS) {
        const [year, month, day] = today.split("-").map(Number);
        const from = new Date(Date.UTC(year, month - 1, day - 89));
        return { from: from.toISOString().slice(0, 10), to: today };
    }

    return uauiPresetRange(id, today);
}

/** The last 30 days compared with the 30 before, for all workspaces. */
export function createDefaultFilters(today: string = getTodayIso()): UaAnalyticsFilters {
    const span = resolvePresetRange("last-30-days", today)!;
    const range: UauiDateRange = { ...span, preset: "last-30-days" };
    return {
        range,
        compare: uauiCompareFor("previous-period", range),
        timeZone: getBrowserTimeZone(),
    };
}

interface StoredFilters {
    range: UauiDateRange;
    /** Null is a comparison the user turned off. */
    compare: UauiCompare | null;
    workspaceId: string | null;
}

const COMPARE_MODES: ReadonlyArray<UauiCompare["mode"]> = ["previous-period", "previous-year", "custom"];

const isIsoDate = (value: unknown): value is string => typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value);

/** The preset ids the page offers, plus the dialog's own "custom". */
const KNOWN_PRESETS: ReadonlyArray<string> = ["last-7-days", "last-30-days", UA_ANALYTICS_PRESET_LAST_90_DAYS, "custom"];

function parseStored(json: string | null, today: string): StoredFilters | undefined {
    if (!json) return undefined;

    const raw = JSON.parse(json) as Partial<StoredFilters> | null;
    const range = raw?.range;
    if (!range || !isIsoDate(range.from) || !isIsoDate(range.to)) return undefined;
    // The range must run forwards and can't end after today (the date range's `maxDate`).
    if (range.from > range.to || range.to > today) return undefined;
    if (range.preset !== undefined && !KNOWN_PRESETS.includes(range.preset)) return undefined;

    const compare = raw?.compare;
    if (
        compare &&
        (!isIsoDate(compare.from) || !isIsoDate(compare.to) || compare.from > compare.to || !COMPARE_MODES.includes(compare.mode))
    ) {
        return undefined;
    }

    return {
        range: { from: range.from, to: range.to, preset: typeof range.preset === "string" ? range.preset : undefined },
        compare: compare ?? null,
        workspaceId: typeof raw?.workspaceId === "string" ? raw.workspaceId : null,
    };
}

/**
 * Reads the filters remembered for this user. Undefined when nothing usable is stored or storage is
 * unavailable. A preset is counted again from `today`, so "Last 7 days" still means the last 7 days tomorrow.
 */
export function readStoredFilters(storageKey: string, today: string = getTodayIso()): UaAnalyticsFilters | undefined {
    let stored: StoredFilters | undefined;
    try {
        stored = parseStored(localStorage.getItem(storageKey), today);
    } catch {
        // Storage blocked, or the stored text isn't JSON: start from the defaults.
        return undefined;
    }
    if (!stored) return undefined;

    const presetSpan = stored.range.preset ? resolvePresetRange(stored.range.preset, today) : undefined;
    const range: UauiDateRange = presetSpan ? { ...presetSpan, preset: stored.range.preset } : stored.range;

    let compare: UauiCompare | undefined;
    if (stored.compare) {
        compare = (stored.compare.mode !== "custom" ? uauiCompareFor(stored.compare.mode, range) : undefined) ?? stored.compare;
    }

    return {
        range,
        compare,
        workspaceId: stored.workspaceId ?? undefined,
        timeZone: getBrowserTimeZone(),
    };
}

export function writeStoredFilters(storageKey: string, filters: UaAnalyticsFilters): void {
    const stored: StoredFilters = {
        range: filters.range,
        compare: filters.compare ?? null,
        workspaceId: filters.workspaceId ?? null,
    };

    try {
        localStorage.setItem(storageKey, JSON.stringify(stored));
    } catch {
        // Storage blocked or full: the choice just isn't remembered.
    }
}
