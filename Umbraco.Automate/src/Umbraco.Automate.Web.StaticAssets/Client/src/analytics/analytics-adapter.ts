import {
    uauiBucketsFor,
    type UauiCompare,
    type UauiDateSpan,
    type UauiGroupBy,
    type UauiSeries,
    type UauiWeekStart,
} from "@umbraco/analytics-ui";
import type {
    AnalyticsActionRowResponseModel,
    AnalyticsAutomationRowResponseModel,
    AnalyticsDayResponseModel,
    AnalyticsPeriodResponseModel,
    AnalyticsSummaryResponseModel,
} from "../api/types.gen.js";

/** Turns a localization key into text. Pass `(key) => this.localize.term(key)` from an element. */
export type UaAnalyticsTranslate = (key: string) => string;

type DayCount = Exclude<keyof AnalyticsDayResponseModel, "date">;

interface SeriesDefinition {
    id: DayCount;
    labelKey: string;
    color: UauiSeries["color"];
}

/** Bottom to top, so the bars add up to the Runs tile. In progress takes palette slot 1. */
const SERIES: ReadonlyArray<SeriesDefinition> = [
    { id: "completed", labelKey: "uaAnalytics_seriesCompleted", color: "positive" },
    { id: "rejected", labelKey: "uaAnalytics_seriesRejected", color: "warning" },
    { id: "cancelled", labelKey: "uaAnalytics_seriesCancelled", color: "default" },
    { id: "failed", labelKey: "uaAnalytics_seriesFailed", color: "danger" },
    { id: "inProgress", labelKey: "uaAnalytics_seriesInProgress", color: 1 },
];

/**
 * The first day of the week for a culture as an ISO weekday (1 Monday to 7 Sunday). Monday when the browser can't say.
 * Umbraco's bare `en` is English (UK), but `Intl` reads it as US English, whose week starts on Sunday, so it is read as `en-GB`.
 */
export function getWeekStart(culture: string): UauiWeekStart {
    try {
        const tag = culture.toLowerCase() === "en" ? "en-GB" : culture;
        const locale = new Intl.Locale(tag) as Intl.Locale & {
            getWeekInfo?: () => { firstDay: number };
            weekInfo?: { firstDay: number };
        };
        const firstDay = (locale.getWeekInfo?.() ?? locale.weekInfo)?.firstDay;
        if (firstDay !== undefined && firstDay >= 1 && firstDay <= 7) return firstDay as UauiWeekStart;
    } catch {
        // An unknown culture tag: fall through to Monday.
    }
    return 1;
}

/**
 * One series per outcome, each with a point per bucket. Days pass straight through at Day level; at Week and
 * Month level the days of each bucket (from `uauiBucketsFor`) are summed, dated at the bucket's start.
 */
export function createSeries(
    period: AnalyticsPeriodResponseModel,
    range: UauiDateSpan,
    groupBy: UauiGroupBy,
    weekStart: UauiWeekStart,
    translate: UaAnalyticsTranslate,
): UauiSeries[] {
    const buckets = uauiBucketsFor(range, groupBy, { weekStart });
    const daysByDate = new Map(period.days.map((day) => [day.date, day]));

    return SERIES.map((definition) => ({
        id: definition.id,
        label: translate(definition.labelKey),
        color: definition.color,
        points: buckets.map((bucket) => ({
            date: bucket.start,
            value: bucket.days.reduce((sum, date) => sum + (daysByDate.get(date)?.[definition.id] ?? 0), 0),
        })),
    }));
}

/** The comparison period's series: same ids as `createSeries`, bucketed over the comparison dates. */
export function createCompareSeries(
    period: AnalyticsPeriodResponseModel | null | undefined,
    compare: UauiCompare | undefined,
    groupBy: UauiGroupBy,
    weekStart: UauiWeekStart,
    translate: UaAnalyticsTranslate,
): UauiSeries[] {
    if (!period || !compare) return [];
    return createSeries(period, compare, groupBy, weekStart, translate);
}

/** Every series is zero: the chart shows "No runs in this period". */
export function hasNoRuns(period: AnalyticsPeriodResponseModel): boolean {
    return period.runs === 0;
}

/** The figures behind the headline tiles. A `previous...` value is undefined when there is nothing to compare with. */
export interface UaAnalyticsKpis {
    runs: number;
    previousRuns?: number;
    /** 0..1, or undefined when nothing finished. */
    successRate?: number;
    previousSuccessRate?: number;
    failedRuns: number;
    previousFailedRuns?: number;
    medianDurationMs?: number;
    previousMedianDurationMs?: number;
    waitingForApproval: number;
}

/** A change in a count or a time is a percentage, which a comparison of 0 can't give. */
const nonZero = (value: number | null | undefined): number | undefined => (value ? value : undefined);

export function createKpis(summary: AnalyticsSummaryResponseModel): UaAnalyticsKpis {
    const { current, compare } = summary;
    return {
        runs: current.runs,
        previousRuns: nonZero(compare?.runs),
        successRate: current.successRate ?? undefined,
        previousSuccessRate: compare?.successRate ?? undefined,
        failedRuns: current.failed,
        previousFailedRuns: nonZero(compare?.failed),
        medianDurationMs: current.medianDurationMs ?? undefined,
        previousMedianDurationMs: nonZero(compare?.medianDurationMs),
        waitingForApproval: summary.waitingForApproval,
    };
}

/** The oldest day (ISO date) still kept by run cleanup; undefined when cleanup is off. */
export function getRetainedFrom(summary: AnalyticsSummaryResponseModel): string | undefined {
    return summary.retainedFrom ? summary.retainedFrom.slice(0, 10) : undefined;
}

/** A run time in the culture's short unit style ("1.8 sec", "1.6 days"), in the largest unit that keeps it at 1 or more. */
export function formatDuration(ms: number, culture: string): string {
    const units: ReadonlyArray<[Intl.NumberFormatOptions["unit"], number]> = [
        ["day", 86_400_000],
        ["hour", 3_600_000],
        ["minute", 60_000],
        ["second", 1_000],
    ];
    const [unit, size] = units.find(([, unitSize]) => ms >= unitSize) ?? (["millisecond", 1] as const);
    const options: Intl.NumberFormatOptions = { style: "unit", unit, unitDisplay: "short", maximumFractionDigits: 1 };
    try {
        return new Intl.NumberFormat(culture, options).format(ms / size);
    } catch {
        return new Intl.NumberFormat(undefined, options).format(ms / size);
    }
}

const ERROR_KEYS: Readonly<Record<string, string>> = {
    Unknown: "uaAnalytics_errorOther",
    Validation: "uaAnalytics_errorValidation",
    Authentication: "uaAnalytics_errorAuthentication",
    RateLimiting: "uaAnalytics_errorRateLimiting",
    Timeout: "uaAnalytics_errorTimeout",
    ServiceUnavailable: "uaAnalytics_errorServiceUnavailable",
    InvalidResponse: "uaAnalytics_errorInvalidResponse",
    Cancelled: "uaAnalytics_errorCancelled",
    ConfigurationError: "uaAnalytics_errorConfigurationError",
};

/** Null (no failures) reads "None", Unknown reads "Other", a category this build doesn't know shows as sent. */
export function formatErrorCategory(category: string | null | undefined, translate: UaAnalyticsTranslate): string {
    if (category == null) return translate("uaAnalytics_errorNone");
    const key = ERROR_KEYS[category];
    return key ? translate(key) : category;
}

/** Action names by alias, for `UaCatalogueRepository.requestActions()` items. */
export function createActionNameLookup(items: ReadonlyArray<{ alias: string; name: string }>): ReadonlyMap<string, string> {
    return new Map(items.map((item) => [item.alias, item.name]));
}

/** An action's name from the catalogue; the alias when the catalogue has none (for example a removed package). */
export function getActionName(alias: string, names: ReadonlyMap<string, string>): string {
    const name = names.get(alias);
    return name ? name : alias;
}

export interface UaAnalyticsAutomationTableRow {
    /** The row key. */
    unique: string;
    name: string;
    workspaceName: string;
    runs: number;
    failed: number;
    /** 0..1, or undefined when nothing finished. */
    successRate?: number;
    avgDurationMs?: number;
    lastRunUtc: string;
}

export interface UaAnalyticsActionTableRow {
    /** The row key. */
    unique: string;
    /** The catalogue name, or the alias when the catalogue doesn't know the action. */
    name: string;
    actionAlias: string;
    runs: number;
    failed: number;
    retries: number;
    failureRate?: number;
    avgDurationMs?: number;
    /** Localized: "None", "Other" or the category's name. */
    topError: string;
}

export function createAutomationRows(
    items: ReadonlyArray<AnalyticsAutomationRowResponseModel>,
): UaAnalyticsAutomationTableRow[] {
    return items.map((item) => ({
        unique: item.automationId,
        name: item.name,
        workspaceName: item.workspaceName,
        runs: item.runs,
        failed: item.failed,
        successRate: item.successRate ?? undefined,
        avgDurationMs: item.avgDurationMs ?? undefined,
        lastRunUtc: item.lastRunUtc,
    }));
}

export function createActionRows(
    items: ReadonlyArray<AnalyticsActionRowResponseModel>,
    names: ReadonlyMap<string, string>,
    translate: UaAnalyticsTranslate,
): UaAnalyticsActionTableRow[] {
    return items.map((item) => ({
        unique: item.actionAlias,
        name: getActionName(item.actionAlias, names),
        actionAlias: item.actionAlias,
        runs: item.runs,
        failed: item.failed,
        retries: item.retries,
        failureRate: item.failureRate ?? undefined,
        avgDurationMs: item.avgDurationMs ?? undefined,
        topError: formatErrorCategory(item.topErrorCategory, translate),
    }));
}
