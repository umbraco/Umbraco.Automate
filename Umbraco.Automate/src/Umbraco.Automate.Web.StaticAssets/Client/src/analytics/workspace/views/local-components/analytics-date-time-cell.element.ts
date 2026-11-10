import type { UauiTableCellElement, UauiTableColumn } from "@umbraco/analytics-ui";
import { css, html, customElement, nothing, property, type PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/**
 * A table cell for a UTC instant, shown in the editor's local time. `uaui-date-cell` reads a date-time as the
 * day it was written in (UTC) and can't show a time, so a run just after local midnight would land on the
 * previous day.
 */
@customElement("ua-analytics-date-time-cell")
export class UaAnalyticsDateTimeCellElement extends UmbLitElement implements UauiTableCellElement {
    @property({ attribute: false })
    column!: UauiTableColumn;

    @property({ attribute: false })
    item!: Record<string, unknown>;

    @property({ attribute: false })
    value: unknown;

    protected override willUpdate(changed: PropertyValues) {
        if (changed.has("column")) this.style.justifyContent = this.column?.align === "end" ? "flex-end" : "flex-start";
    }

    override render() {
        if (typeof this.value !== "string") return nothing;

        const date = new Date(this.value);
        if (Number.isNaN(date.getTime())) return nothing;

        return html`${this.localize.date(date, { dateStyle: "medium", timeStyle: "short" })}`;
    }

    static override styles = css`
        :host {
            display: flex;
            align-items: center;
            font-variant-numeric: tabular-nums;
        }
    `;
}

export default UaAnalyticsDateTimeCellElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-date-time-cell": UaAnalyticsDateTimeCellElement;
    }
}
