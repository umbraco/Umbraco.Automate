import type { UauiTableCellElement, UauiTableColumn } from "@umbraco/analytics-ui";
import { css, html, customElement, nothing, property, type PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { formatDuration } from "../../../analytics-adapter.js";

/**
 * A table cell for a run time held in milliseconds. `uaui-number-cell` only takes `Intl.NumberFormat` options,
 * which can't pick the unit per value ("2.4 sec", "1.6 days"), so this formats with `formatDuration` and
 * adds the table's change badge like the built-in cells do.
 */
@customElement("ua-analytics-duration-cell")
export class UaAnalyticsDurationCellElement extends UmbLitElement implements UauiTableCellElement {
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
        if (typeof this.value !== "number") return nothing;

        return html`<span class="value">${formatDuration(this.value, this.localize.lang())}</span>
            <uaui-table-compare-badge .column=${this.column} .item=${this.item} .value=${this.value}></uaui-table-compare-badge>`;
    }

    static override styles = css`
        :host {
            display: flex;
            align-items: center;
            flex-wrap: wrap;
            gap: var(--uui-size-space-2);
            font-variant-numeric: tabular-nums;
        }
    `;
}

export default UaAnalyticsDurationCellElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-duration-cell": UaAnalyticsDurationCellElement;
    }
}
