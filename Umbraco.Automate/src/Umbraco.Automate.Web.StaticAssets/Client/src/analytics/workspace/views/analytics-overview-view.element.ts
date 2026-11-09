import {
    uauiFormatDateRange,
    type UauiGroupBy,
    type UauiGroupByChangeDetail,
    type UauiSeries,
} from "@umbraco/analytics-ui";
import { UMB_DASHBOARD_PATH_PATTERN } from "@umbraco-cms/backoffice/dashboard";
import { css, html, customElement, nothing, state, type PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { AnalyticsSummaryResponseModel } from "../../../api/types.gen.js";
import { UA_APPROVALS_DASHBOARD_PATHNAME } from "../../../approval/dashboard/constants.js";
import { UA_SECTION_PATHNAME } from "../../../section/constants.js";
import {
    createCompareSeries,
    createKpis,
    createSeries,
    formatDuration,
    getRetainedFrom,
    getWeekStart,
    hasNoRuns,
} from "../../analytics-adapter.js";
import { UaAnalyticsServerDataSource } from "../../repository/analytics.server.data-source.js";
import type { UaAnalyticsFilters } from "../analytics-filters.js";
import { UA_ANALYTICS_WORKSPACE_CONTEXT } from "../analytics-workspace.context-token.js";

const APPROVALS_HREF = UMB_DASHBOARD_PATH_PATTERN.generateAbsolute({
    sectionName: UA_SECTION_PATHNAME,
    dashboardPathname: UA_APPROVALS_DASHBOARD_PATHNAME,
});

/** The answer together with the filters it was asked for, so the chart never mixes old data with a new range. */
interface LoadedSummary {
    summary: AnalyticsSummaryResponseModel;
    filters: UaAnalyticsFilters;
}

@customElement("ua-analytics-overview-view")
export class UaAnalyticsOverviewViewElement extends UmbLitElement {
    // One instance for the view's life: its stale-answer counters live on it.
    #dataSource = new UaAnalyticsServerDataSource(this);

    @state()
    private _filters?: UaAnalyticsFilters;

    @state()
    private _loaded?: LoadedSummary;

    @state()
    private _loading = false;

    @state()
    private _error = false;

    @state()
    private _groupBy: UauiGroupBy = "day";

    private _series: UauiSeries[] = [];
    private _compareSeries: UauiSeries[] = [];

    constructor() {
        super();
        this.consumeContext(UA_ANALYTICS_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;

            this.observe(
                context.filters,
                (filters) => {
                    this._filters = filters;
                    this.#loadSummary();
                },
                "_observeFilters",
            );
        });
    }

    async #loadSummary() {
        const filters = this._filters;
        if (!filters) return;

        this._loading = true;
        this._error = false;

        const result = await this.#dataSource.getSummary(filters);
        // A newer request has been started; it will report.
        if (result.stale) return;

        this._loading = false;
        if (result.error || !result.data) {
            this._error = true;
            return;
        }

        this._loaded = { summary: result.data, filters };
    }

    #onGroupByChange(event: CustomEvent<UauiGroupByChangeDetail>) {
        this._groupBy = event.detail.value;
    }

    protected override willUpdate(changed: PropertyValues) {
        if (!changed.has("_loaded") && !changed.has("_groupBy")) return;

        const loaded = this._loaded;
        // Nothing ran: leave the series empty so the chart shows its empty state.
        if (!loaded || hasNoRuns(loaded.summary.current)) {
            this._series = [];
            this._compareSeries = [];
            return;
        }

        const { summary, filters } = loaded;
        const translate = (key: string) => this.localize.term(key);
        const weekStart = getWeekStart(this.localize.lang());
        this._series = createSeries(summary.current, filters.range, this._groupBy, weekStart, translate);
        this._compareSeries = createCompareSeries(summary.compare, filters.compare, this._groupBy, weekStart, translate);
    }

    /** No answer yet and no failure: the workspace is still loading or the first request is on its way. */
    get #isLoading() {
        return this._loading || (!this._loaded && !this._error);
    }

    #renderRetentionNotice() {
        // After a failed reload the old answer no longer matches the filters.
        if (!this._loaded || this._error) return nothing;

        const retainedFrom = getRetainedFrom(this._loaded.summary);
        if (!retainedFrom || this._loaded.filters.range.from >= retainedFrom) return nothing;

        const [year, month, day] = retainedFrom.split("-").map(Number);
        const date = this.localize.date(new Date(year, month - 1, day), { dateStyle: "medium" });
        const key = this._loaded.filters.compare ? "uaAnalytics_retentionNoticeCompared" : "uaAnalytics_retentionNotice";
        return html`<div class="notice" data-notice="retention">
            <uui-icon name="icon-info"></uui-icon>
            <span>${this.localize.term(key, date)}</span>
        </div>`;
    }

    #renderTiles() {
        const loaded = this._loaded;
        const kpis = loaded ? createKpis(loaded.summary) : undefined;
        const culture = this.localize.lang();
        const common = { loading: this.#isLoading, error: this._error };
        // The comparison was asked for but the server dropped it: its runs are deleted (SPEC R7).
        const comparisonDeleted = !!loaded?.filters.compare && !loaded.summary.compare;

        return html`<div class="tiles">
            <uaui-kpi-tile
                data-tile="runs"
                icon="icon-play"
                .label=${this.localize.term("uaAnalytics_tileRuns")}
                .value=${kpis?.runs}
                .previous=${kpis?.previousRuns}
                .detail=${comparisonDeleted ? this.localize.term("uaAnalytics_tileNoComparison") : undefined}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
            <uaui-kpi-tile
                data-tile="success-rate"
                icon="icon-check"
                delta-unit="points"
                .label=${this.localize.term("uaAnalytics_tileSuccessRate")}
                .value=${kpis?.successRate}
                .valueFormat=${{ style: "percent", maximumFractionDigits: 1 }}
                .previous=${kpis?.previousSuccessRate}
                ?no-data=${!!kpis && kpis.successRate === undefined}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
            <uaui-kpi-tile
                data-tile="failed-runs"
                icon="icon-alert"
                lower-is-better
                .label=${this.localize.term("uaAnalytics_tileFailedRuns")}
                .value=${kpis?.failedRuns}
                .previous=${kpis?.previousFailedRuns}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
            <uaui-kpi-tile
                data-tile="median-run-time"
                icon="icon-time"
                lower-is-better
                .label=${this.localize.term("uaAnalytics_tileMedianRunTime")}
                .value=${kpis?.medianDurationMs === undefined ? undefined : formatDuration(kpis.medianDurationMs, culture)}
                .current=${kpis?.medianDurationMs}
                .previous=${kpis?.previousMedianDurationMs}
                ?no-data=${!!kpis && kpis.medianDurationMs === undefined}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
            <uaui-kpi-tile
                data-tile="waiting-for-approval"
                icon="icon-hand-active"
                href=${APPROVALS_HREF}
                .label=${this.localize.term("uaAnalytics_tileWaitingForApproval")}
                .value=${kpis?.waitingForApproval}
                .detail=${this.localize.term("uaAnalytics_tileRightNow")}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
        </div>`;
    }

    #renderChartBox() {
        const loaded = this._loaded;
        const compare = loaded?.filters.compare;
        const compareLabel =
            loaded && compare && loaded.summary.compare ? uauiFormatDateRange(this.localize, compare) : undefined;

        return html`<uui-box class="flush" data-box="runs-over-time" headline=${this.localize.term("uaAnalytics_runsOverTime")}>
            <uaui-group-by slot="header-actions" .value=${this._groupBy} @change=${this.#onGroupByChange}></uaui-group-by>
            <uaui-time-series-chart
                kind="stacked-bar"
                .series=${this._series}
                .compareSeries=${this._compareSeries}
                .range=${loaded?.filters.range}
                .compare=${loaded?.summary.compare ? compare : undefined}
                .compareLabel=${compareLabel}
                .groupBy=${this._groupBy}
                .weekStart=${getWeekStart(this.localize.lang())}
                .loading=${this.#isLoading}
                .error=${this._error}
                .errorHeadline=${this.localize.term("uaAnalytics_runsOverTimeErrorHeadline")}
                .errorDescription=${this.localize.term("uaAnalytics_runsOverTimeErrorDescription")}
                .emptyHeadline=${this.localize.term("uaAnalytics_runsOverTimeEmptyHeadline")}
                .emptyDescription=${this.localize.term("uaAnalytics_runsOverTimeEmptyDescription")}
                retryable
                @retry=${() => this.#loadSummary()}></uaui-time-series-chart>
        </uui-box>`;
    }

    override render() {
        return html`${this.#renderRetentionNotice()} ${this.#renderTiles()} ${this.#renderChartBox()}`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
                padding: var(--uui-size-layout-1);
            }

            .notice {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
                color: var(--uui-color-text-alt);
            }

            .tiles {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(var(--uui-size-64), 1fr));
                gap: var(--uui-size-space-4);
            }

            uui-box.flush {
                --uui-box-default-padding: 0;
            }
        `,
    ];
}

export default UaAnalyticsOverviewViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-overview-view": UaAnalyticsOverviewViewElement;
    }
}
