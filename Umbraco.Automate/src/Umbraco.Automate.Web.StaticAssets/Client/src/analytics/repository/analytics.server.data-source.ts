import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute, UmbApiError } from "@umbraco-cms/backoffice/resources";
import { MetricsService } from "../../api/sdk.gen.js";
import type {
    AnalyticsActionsResponseModel,
    AnalyticsAutomationsResponseModel,
    AnalyticsSummaryResponseModel,
} from "../../api/types.gen.js";
import type { UaAnalyticsFilters } from "../workspace/analytics-filters.js";

/**
 * The outcome of one request. `stale` is true when a newer request of the same kind was started while
 * this one was in flight: the caller must ignore it (no data, no error) and wait for the newer one.
 */
export interface UaAnalyticsResult<T> {
    data?: T;
    error?: unknown;
    stale?: boolean;
    rejection?: UaAnalyticsRejection;
}

/** Set when the server answered 400: the filters were refused, so asking again can't help. */
export interface UaAnalyticsRejection {
    /** The ProblemDetails `operationStatus` (`RangeTooLong`, `CompareLengthMismatch`, ...), when the answer had one. */
    operationStatus?: string;
}

function failure<T>(error: unknown): UaAnalyticsResult<T> {
    const result: UaAnalyticsResult<T> = { error: error ?? new Error("No data") };
    // The generated client resolves a failed request to its ProblemDetails body (no `throwOnError`), and
    // tryExecute only wraps a rejection in an UmbApiError, so the body can arrive either way.
    const body = (UmbApiError.isUmbApiError(error) ? error.problemDetails : error) as
        | { status?: unknown; operationStatus?: unknown }
        | undefined;
    if (body?.status === 400) {
        result.rejection = { operationStatus: typeof body.operationStatus === "string" ? body.operationStatus : undefined };
    }
    return result;
}

interface AnalyticsQuery {
    from: string;
    to: string;
    timeZone: string;
    compareFrom?: string;
    compareTo?: string;
    workspaceId?: string;
}

function toQuery(filters: UaAnalyticsFilters): AnalyticsQuery {
    return {
        from: filters.range.from,
        to: filters.range.to,
        timeZone: filters.timeZone,
        compareFrom: filters.compare?.from,
        compareTo: filters.compare?.to,
        workspaceId: filters.workspaceId,
    };
}

/**
 * Calls the three analytics endpoints. Each endpoint has its own request counter, so a slow answer for
 * an old range never overwrites the answer for the current one, and one slow box never marks another stale.
 * Errors come back as `{ error }` without a notification: each box shows its own error state with Retry.
 */
export class UaAnalyticsServerDataSource {
    #host: UmbControllerHost;
    #summaryRequest = 0;
    #automationsRequest = 0;
    #actionsRequest = 0;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getSummary(filters: UaAnalyticsFilters): Promise<UaAnalyticsResult<AnalyticsSummaryResponseModel>> {
        const request = ++this.#summaryRequest;
        const { data, error } = await tryExecute(
            this.#host,
            MetricsService.getMetricsAnalyticsSummary({ query: toQuery(filters) }),
            { disableNotifications: true },
        );
        if (request !== this.#summaryRequest) return { stale: true };
        return error || !data ? failure(error) : { data };
    }

    async getAutomations(filters: UaAnalyticsFilters): Promise<UaAnalyticsResult<AnalyticsAutomationsResponseModel>> {
        const request = ++this.#automationsRequest;
        const { data, error } = await tryExecute(
            this.#host,
            MetricsService.getMetricsAnalyticsAutomations({ query: toQuery(filters) }),
            { disableNotifications: true },
        );
        if (request !== this.#automationsRequest) return { stale: true };
        return error || !data ? failure(error) : { data };
    }

    async getActions(filters: UaAnalyticsFilters): Promise<UaAnalyticsResult<AnalyticsActionsResponseModel>> {
        const request = ++this.#actionsRequest;
        const { data, error } = await tryExecute(
            this.#host,
            MetricsService.getMetricsAnalyticsActions({ query: toQuery(filters) }),
            { disableNotifications: true },
        );
        if (request !== this.#actionsRequest) return { stale: true };
        return error || !data ? failure(error) : { data };
    }
}
