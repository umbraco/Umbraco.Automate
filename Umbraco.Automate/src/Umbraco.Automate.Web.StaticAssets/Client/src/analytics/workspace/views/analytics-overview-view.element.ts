import {
    UAUI_TABLE_CELL,
    uauiFormatDateRange,
    type UauiGroupBy,
    type UauiGroupByChangeDetail,
    type UauiSeries,
    type UauiTableColumn,
    type UauiTableSort,
} from "@umbraco/analytics-ui";
import { UMB_DASHBOARD_PATH_PATTERN } from "@umbraco-cms/backoffice/dashboard";
import { css, html, customElement, nothing, state, type PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type {
    AnalyticsActionsResponseModel,
    AnalyticsAutomationsResponseModel,
    AnalyticsSummaryResponseModel,
} from "../../../api/types.gen.js";
import { UA_APPROVALS_DASHBOARD_PATHNAME } from "../../../approval/dashboard/constants.js";
import { UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../../../automation/workspace/automation/paths.js";
import { UaCatalogueRepository } from "../../../catalogue/repository/catalogue.repository.js";
import { UA_SECTION_PATHNAME } from "../../../section/constants.js";
import {
    createActionNameLookup,
    createActionRows,
    createAutomationRows,
    createCompareSeries,
    createKpis,
    createSeries,
    formatDuration,
    getRetainedFrom,
    getWeekStart,
    hasNoRuns,
} from "../../analytics-adapter.js";
import { UaAnalyticsServerDataSource } from "../../repository/analytics.server.data-source.js";
import type { UaAnalyticsRejection } from "../../repository/analytics.server.data-source.js";
import { UA_ANALYTICS_MAX_RANGE_DAYS, type UaAnalyticsFilters } from "../analytics-filters.js";
import { UA_ANALYTICS_WORKSPACE_CONTEXT } from "../analytics-workspace.context-token.js";
import "./local-components/analytics-action-name-cell.element.js";
import "./local-components/analytics-date-time-cell.element.js";
import "./local-components/analytics-duration-cell.element.js";
import "./local-components/analytics-error-cell.element.js";

const APPROVALS_HREF = UMB_DASHBOARD_PATH_PATTERN.generateAbsolute({
    sectionName: UA_SECTION_PATHNAME,
    dashboardPathname: UA_APPROVALS_DASHBOARD_PATHNAME,
});

/** The answer together with the filters it was asked for, so the chart never mixes old data with a new range. */
interface LoadedSummary {
    summary: AnalyticsSummaryResponseModel;
    filters: UaAnalyticsFilters;
}

interface LoadedAutomations {
    answer: AnalyticsAutomationsResponseModel;
}

interface LoadedActions {
    answer: AnalyticsActionsResponseModel;
    /** Action names by alias; empty when the catalogue couldn't be read, so rows show their alias. */
    names: ReadonlyMap<string, string>;
}

/** Sorts two ISO date-times by the instant they name; a missing or unreadable one goes last. */
const compareInstants = (a: unknown, b: unknown) => {
    const time = (value: unknown) => (typeof value === "string" ? Date.parse(value) : Number.NaN);
    const [first, second] = [time(a), time(b)];
    if (Number.isNaN(first)) return Number.isNaN(second) ? 0 : 1;
    if (Number.isNaN(second)) return -1;
    return first - second;
};

/** The text for each `operationStatus` the analytics endpoints answer a 400 with; anything else gets the general one. */
const REJECTION_KEYS: Readonly<Record<string, string>> = {
    RangeTooLong: "uaAnalytics_rejectedRangeTooLong",
    RangeBackwards: "uaAnalytics_rejectedRangeBackwards",
    CompareLengthMismatch: "uaAnalytics_rejectedCompareLength",
    IncompleteCompare: "uaAnalytics_rejectedCompareLength",
    UnknownTimeZone: "uaAnalytics_rejectedTimeZone",
    DateOutOfRange: "uaAnalytics_rejectedDateOutOfRange",
};

type AutomationRow =ReturnType<typeof createAutomationRows>[number];

/** A table row: the adapter's row plus the link to its automation, which the link cell reads from `href`. */
const withHref = (row: AutomationRow) => ({
    ...row,
    href: UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: row.unique }),
});

@customElement("ua-analytics-overview-view")
export class UaAnalyticsOverviewViewElement extends UmbLitElement {
    // One instance for the view's life: its stale-answer counters live on it.
    #dataSource = new UaAnalyticsServerDataSource(this);
    // One instance for the view's life: it caches the catalogue, which only supplies action names.
    #catalogueRepository = new UaCatalogueRepository(this);

    @state()
    private _filters?: UaAnalyticsFilters;

    @state()
    private _loaded?: LoadedSummary;

    @state()
    private _loading = false;

    @state()
    private _error = false;

    @state()
    private _automations?: LoadedAutomations;

    @state()
    private _automationsLoading = false;

    @state()
    private _automationsError = false;

    @state()
    private _automationsSort: UauiTableSort = { key: "runs", direction: "desc" };

    @state()
    private _actions?: LoadedActions;

    @state()
    private _actionsLoading = false;

    @state()
    private _actionsError = false;

    @state()
    private _actionsSort: UauiTableSort = { key: "runs", direction: "desc" };

    @state()
    private _groupBy: UauiGroupBy = "day";

    private _series: UauiSeries[] = [];
    private _compareSeries: UauiSeries[] = [];
    private _automationRows: ReturnType<typeof withHref>[] = [];
    private _automationCompareRows: ReturnType<typeof withHref>[] = [];
    private _automationColumns: UauiTableColumn[] = [];
    private _actionRows: ReturnType<typeof createActionRows> = [];
    private _actionCompareRows: ReturnType<typeof createActionRows> = [];
    private _actionColumns: UauiTableColumn[] = [];
    #lang = "";

    @state()
    private _rangeClamped = false;

    /** Set while a box shows the server's 400: the filters were refused, which a retry can't fix. */
    @state()
    private _summaryRejection?: UaAnalyticsRejection;

    @state()
    private _automationsRejection?: UaAnalyticsRejection;

    @state()
    private _actionsRejection?: UaAnalyticsRejection;

    constructor() {
        super();
        this.consumeContext(UA_ANALYTICS_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;

            this.observe(
                context.filters,
                (filters) => {
                    this._filters = filters;
                    this._summaryRejection = this._automationsRejection = this._actionsRejection = undefined;
                    this.#loadSummary();
                    this.#loadAutomations();
                    this.#loadActions();
                },
                "_observeFilters",
            );
            this.observe(context.rangeClamped, (clamped) => (this._rangeClamped = clamped), "_observeRangeClamped");
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
            this._summaryRejection = result.rejection;
            return;
        }

        this._loaded = { summary: result.data, filters };
    }

    async #loadAutomations() {
        const filters = this._filters;
        if (!filters) return;

        this._automationsLoading = true;
        this._automationsError = false;

        const result = await this.#dataSource.getAutomations(filters);
        // A newer request has been started; it will report.
        if (result.stale) return;

        this._automationsLoading = false;
        if (result.error || !result.data) {
            this._automationsError = true;
            this._automationsRejection = result.rejection;
            return;
        }

        this._automations = { answer: result.data };
    }

    async #loadActions() {
        const filters = this._filters;
        if (!filters) return;

        this._actionsLoading = true;
        this._actionsError = false;

        // The names are only decoration: if the catalogue fails, rows fall back to the alias.
        const [result, catalogue] = await Promise.all([
            this.#dataSource.getActions(filters),
            this.#catalogueRepository.requestActions(),
        ]);
        // A newer request has been started; it will report.
        if (result.stale) return;

        this._actionsLoading = false;
        if (result.error || !result.data) {
            this._actionsError = true;
            this._actionsRejection = result.rejection;
            return;
        }

        this._actions = { answer: result.data, names: createActionNameLookup(catalogue.data ?? []) };
    }

    #onGroupByChange(event: CustomEvent<UauiGroupByChangeDetail>) {
        this._groupBy = event.detail.value;
    }

    protected override willUpdate(changed: PropertyValues) {
        // Headers, series names and error text are translated when built, so a language switch rebuilds them.
        const lang = this.localize.lang();
        const langChanged = lang !== this.#lang;
        this.#lang = lang;

        // The columns don't depend on the answer, so the table has its headers and loading rows before it arrives.
        if (langChanged || this._automationColumns.length === 0) this._automationColumns = this.#getAutomationColumns();

        if (changed.has("_automations")) {
            // Worked out here, not in render, so the table keeps one rows array (it resets its page on a new one).
            const answer = this._automations?.answer;
            this._automationRows = createAutomationRows(answer?.items ?? []).map(withHref);
            this._automationCompareRows = createAutomationRows(answer?.compareItems ?? []).map(withHref);        }

        if (langChanged || this._actionColumns.length === 0) this._actionColumns = this.#getActionColumns();

        if (changed.has("_actions") || langChanged) {
            const translate = (key: string) => this.localize.term(key);
            const names = this._actions?.names ?? new Map<string, string>();
            const answer = this._actions?.answer;
            this._actionRows = createActionRows(answer?.items ?? [], names, translate);
            this._actionCompareRows = createActionRows(answer?.compareItems ?? [], names, translate);
        }

        if (!langChanged && !changed.has("_loaded") && !changed.has("_groupBy")) return;

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

    /** Why a box couldn't load: the filters were refused (400), or the request didn't get through. */
    #errorDescription(rejection?: UaAnalyticsRejection) {
        if (!rejection) return this.localize.term("uaAnalytics_errorDescription");

        const status = rejection.operationStatus;
        return this.localize.term((status && REJECTION_KEYS[status]) || "uaAnalytics_rejectedDescription");
    }

    #renderRangeClampedNotice() {
        if (!this._rangeClamped) return nothing;

        return html`<div class="notice" data-notice="range-clamped">
            <uui-icon name="icon-info"></uui-icon>
            <span>${this.localize.term("uaAnalytics_rangeClamped", UA_ANALYTICS_MAX_RANGE_DAYS)}</span>
        </div>`;
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
                .label=${this.localize.term("uaAnalytics_runs")}
                .value=${kpis?.runs}
                .previous=${kpis?.previousRuns}
                .detail=${comparisonDeleted ? this.localize.term("uaAnalytics_tileNoComparison") : undefined}
                .loading=${common.loading}
                .error=${common.error}></uaui-kpi-tile>
            <uaui-kpi-tile
                data-tile="success-rate"
                icon="icon-check"
                delta-unit="points"
                .label=${this.localize.term("uaAnalytics_successRate")}
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

        return html`<uui-box class="flush" data-box="runs-over-time" headline-variant="h4" headline=${this.localize.term("uaAnalytics_runsOverTime")}>
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
                .errorDescription=${this.#errorDescription(this._summaryRejection)}
                .emptyHeadline=${this.localize.term("uaAnalytics_runsOverTimeEmptyHeadline")}
                .emptyDescription=${this.localize.term("uaAnalytics_runsOverTimeEmptyDescription")}
                retryable
                @retry=${() => this.#loadSummary()}></uaui-time-series-chart>
        </uui-box>`;
    }

    /** Like `#isLoading`, for the automations request. */
    get #isLoadingAutomations() {
        return this._automationsLoading || (!this._automations && !this._automationsError);
    }

    #getAutomationColumns(): UauiTableColumn[] {
        const term = (key: string) => this.localize.term(key);
        return [
            {
                key: "name",
                label: term("uaAnalytics_columnAutomation"),
                elementName: UAUI_TABLE_CELL.link,
                meta: { hrefKey: "href", secondaryKey: "workspaceName" },
                searchable: true,
                sortable: true,
            },
            {
                key: "runs",
                label: term("uaAnalytics_runs"),
                elementName: UAUI_TABLE_CELL.share,
                sortable: true,
                sortAs: "number",
            },
            {
                key: "successRate",
                label: term("uaAnalytics_successRate"),
                elementName: UAUI_TABLE_CELL.percent,
                meta: { deltaUnit: "points" },
                valueFormat: { style: "percent", maximumFractionDigits: 1 },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "failed",
                label: term("uaAnalytics_columnFailed"),
                elementName: UAUI_TABLE_CELL.number,
                meta: { lowerIsBetter: true },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "avgDurationMs",
                label: term("uaAnalytics_columnAvgRunTime"),
                elementName: "ua-analytics-duration-cell",
                meta: { lowerIsBetter: true },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "lastRunUtc",
                label: term("uaAnalytics_columnLastRun"),
                elementName: "ua-analytics-date-time-cell",
                sortable: true,
                sortAs: compareInstants,
            },
        ];
    }

    #renderAutomationsBox() {
        return html`<uui-box class="flush" data-box="automations" headline-variant="h4" headline=${this.localize.term("uaAnalytics_automationsTitle")}>
            <uaui-data-table
                data-table="automations"
                mode="client"
                row-key="unique"
                reset-page-on-rows
                .label=${this.localize.term("uaAnalytics_automationsTitle")}
                .columns=${this._automationColumns}
                .pageSize=${100}
                .rows=${this._automationRows}
                .compareRows=${this._automationCompareRows}
                .sort=${this._automationsSort}
                .loading=${this.#isLoadingAutomations}
                .error=${this._automationsError}
                .errorHeadline=${this.localize.term("uaAnalytics_automationsErrorHeadline")}
                .errorDescription=${this.#errorDescription(this._automationsRejection)}
                .emptyHeadline=${this.localize.term("uaAnalytics_automationsEmptyHeadline")}
                .emptyDescription=${this.localize.term("uaAnalytics_automationsEmptyDescription")}
                retryable
                @sort-change=${(event: CustomEvent<UauiTableSort>) => (this._automationsSort = event.detail)}
                @retry=${() => this.#loadAutomations()}>
                <uaui-search-field slot="toolbar-start" data-search="automations"></uaui-search-field>
            </uaui-data-table>
        </uui-box>`;
    }

    /** Like `#isLoading`, for the actions request. */
    get #isLoadingActions() {
        return this._actionsLoading || (!this._actions && !this._actionsError);
    }

    #getActionColumns(): UauiTableColumn[] {
        const term = (key: string) => this.localize.term(key);
        return [
            {
                key: "name",
                label: term("uaAnalytics_columnAction"),
                elementName: "ua-analytics-action-name-cell",
                searchable: true,
                sortable: true,
            },
            {
                key: "runs",
                label: term("uaAnalytics_runs"),
                elementName: UAUI_TABLE_CELL.share,
                sortable: true,
                sortAs: "number",
            },
            {
                key: "failureRate",
                label: term("uaAnalytics_columnFailureRate"),
                elementName: UAUI_TABLE_CELL.percent,
                meta: { deltaUnit: "points", lowerIsBetter: true },
                valueFormat: { style: "percent", maximumFractionDigits: 1 },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "retries",
                label: term("uaAnalytics_columnRetries"),
                elementName: UAUI_TABLE_CELL.number,
                meta: { lowerIsBetter: true },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "avgDurationMs",
                label: term("uaAnalytics_columnAvgRunTime"),
                elementName: "ua-analytics-duration-cell",
                meta: { lowerIsBetter: true },
                align: "end",
                sortable: true,
                sortAs: "number",
            },
            {
                key: "topError",
                label: term("uaAnalytics_columnMostCommonError"),
                elementName: "ua-analytics-error-cell",
                sortable: true,
            },
        ];
    }

    #renderActionsBox() {
        return html`<uui-box class="flush" data-box="actions" headline-variant="h4" headline=${this.localize.term("uaAnalytics_actionsTitle")}>
            <uaui-data-table
                data-table="actions"
                mode="client"
                row-key="unique"
                reset-page-on-rows
                .label=${this.localize.term("uaAnalytics_actionsTitle")}
                .columns=${this._actionColumns}
                .pageSize=${100}
                .rows=${this._actionRows}
                .compareRows=${this._actionCompareRows}
                .sort=${this._actionsSort}
                .loading=${this.#isLoadingActions}
                .error=${this._actionsError}
                .errorHeadline=${this.localize.term("uaAnalytics_actionsErrorHeadline")}
                .errorDescription=${this.#errorDescription(this._actionsRejection)}
                .emptyHeadline=${this.localize.term("uaAnalytics_actionsEmptyHeadline")}
                .emptyDescription=${this.localize.term("uaAnalytics_actionsEmptyDescription")}
                retryable
                @sort-change=${(event: CustomEvent<UauiTableSort>) => (this._actionsSort = event.detail)}
                @retry=${() => this.#loadActions()}>
                <uaui-search-field slot="toolbar-start" data-search="actions"></uaui-search-field>
            </uaui-data-table>
        </uui-box>`;
    }

    override render() {
        return html`${this.#renderRangeClampedNotice()} ${this.#renderRetentionNotice()} ${this.#renderTiles()} ${this.#renderChartBox()}
        ${this.#renderAutomationsBox()} ${this.#renderActionsBox()}`;
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
