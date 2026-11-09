import type { UauiDateRangeChangeDetail } from "@umbraco/analytics-ui";
import { css, html, customElement, nothing, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UUISelectElement } from "@umbraco-cms/backoffice/external/uui";
import type { UaWorkspaceItemModel } from "../../workspace-management/types.js";
import { createAnalyticsPresets, getTodayIso, type UaAnalyticsFilters } from "./analytics-filters.js";
import type { UaAnalyticsWorkspaceContext } from "./analytics-workspace.context.js";
import { UA_ANALYTICS_WORKSPACE_CONTEXT } from "./analytics-workspace.context-token.js";

@customElement("ua-analytics-workspace")
export class UaAnalyticsWorkspaceElement extends UmbLitElement {
    #context?: UaAnalyticsWorkspaceContext;

    @state()
    private _filters?: UaAnalyticsFilters;

    @state()
    private _workspaces: Array<UaWorkspaceItemModel> = [];

    constructor() {
        super();
        this.consumeContext(UA_ANALYTICS_WORKSPACE_CONTEXT, (context) => {
            this.#context = context;
            if (!context) return;

            this.observe(context.filters, (filters) => (this._filters = filters), "_observeFilters");
            this.observe(context.workspaces, (workspaces) => (this._workspaces = workspaces), "_observeWorkspaces");
        });
    }

    #onWorkspaceChange(event: Event) {
        this.#context?.setWorkspace((event.target as UUISelectElement).value as string);
    }

    #onDateRangeChange(event: CustomEvent<UauiDateRangeChangeDetail>) {
        this.#context?.setDateRange(event.detail.value, event.detail.compare);
    }

    #renderWorkspaceSelect() {
        // Nothing to choose between with a single workspace.
        if (this._workspaces.length < 2) return nothing;

        const options = [
            {
                name: this.localize.term("uaAnalytics_allWorkspaces"),
                value: "",
                selected: !this._filters?.workspaceId,
            },
            ...this._workspaces.map((workspace) => ({
                name: workspace.name,
                value: workspace.unique,
                selected: workspace.unique === this._filters?.workspaceId,
            })),
        ];

        return html`<uui-select
            label=${this.localize.term("uaLabels_workspace")}
            .options=${options}
            @change=${this.#onWorkspaceChange}></uui-select>`;
    }

    #renderDateRange() {
        if (!this._filters) return nothing;

        return html`<uaui-date-range
            data-preset=${this._filters.range.preset ?? "custom"}
            .value=${this._filters.range}
            .compare=${this._filters.compare}
            .presets=${createAnalyticsPresets(this.localize.term("uaAnalytics_last90Days"))}
            .maxDate=${getTodayIso()}
            @change=${this.#onDateRangeChange}></uaui-date-range>`;
    }

    override render() {
        return html`<umb-workspace-editor
            headline=${this.localize.term("uaMenu_analytics")}
            .enforceNoFooter=${true}>
            <div slot="action-menu" class="header-controls">
                ${this.#renderWorkspaceSelect()} ${this.#renderDateRange()}
            </div>
        </umb-workspace-editor>`;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                width: 100%;
                height: 100%;
            }

            .header-controls {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-4);
                margin-left: auto;
                padding-inline-end: var(--uui-size-layout-1);
            }

            uui-select {
                width: var(--uui-size-64);
            }
        `,
    ];
}

export default UaAnalyticsWorkspaceElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-workspace": UaAnalyticsWorkspaceElement;
    }
}
