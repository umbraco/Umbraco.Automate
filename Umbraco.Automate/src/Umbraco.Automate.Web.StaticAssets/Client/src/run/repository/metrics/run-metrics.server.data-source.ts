import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { MetricsService } from "../../../api/sdk.gen.js";
import type { UaRunSummary } from "../../types.js";

/**
 * Fetches run-status counts across all automations the current user can access, via the
 * `GET /metrics` endpoint (scoped server-side). Used by the section dashboard's status cards.
 */
export class UaRunMetricsServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getSummary(): Promise<{ data?: UaRunSummary; error?: unknown }> {
        const { data, error } = await tryExecute(this.#host, MetricsService.getMetrics());

        if (error || !data) {
            return { error };
        }

        // The API serialises the run-status dictionary keys with the camelCase naming policy
        // (e.g. "failed", "running"), unlike the PascalCase status enum values used elsewhere.
        // Normalise to lower-case so callers can look up statuses without depending on that.
        const byStatus: Record<string, number> = {};
        for (const [status, count] of Object.entries(data.byStatus)) {
            byStatus[status.toLowerCase()] = count;
        }

        return { data: { totalRuns: data.totalRuns, byStatus } };
    }
}
