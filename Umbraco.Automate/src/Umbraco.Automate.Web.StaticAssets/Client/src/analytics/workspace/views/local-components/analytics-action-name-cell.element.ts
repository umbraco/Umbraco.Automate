import type { UauiTableCellElement, UauiTableColumn } from "@umbraco/analytics-ui";
import { css, html, customElement, property, type PropertyValues } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { getErrorCategoryKey } from "../../../analytics-adapter.js";

/**
 * The action's name. `uaui-data-table` can't put attributes on a row, so the cell carries the row's
 * `data-alias` and `data-top-error` (read from the row) for tests and tooling to find the row by.
 */
@customElement("ua-analytics-action-name-cell")
export class UaAnalyticsActionNameCellElement extends UmbLitElement implements UauiTableCellElement {
    @property({ attribute: false })
    column!: UauiTableColumn;

    @property({ attribute: false })
    item!: Record<string, unknown>;

    @property({ attribute: false })
    value: unknown;

    protected override willUpdate(changed: PropertyValues) {
        if (!changed.has("item")) return;

        const alias = this.item?.actionAlias;
        if (typeof alias === "string") this.setAttribute("data-alias", alias);
        else this.removeAttribute("data-alias");

        const category = this.item?.topErrorCategory;
        this.setAttribute("data-top-error", getErrorCategoryKey(typeof category === "string" ? category : null));
    }

    override render() {
        return html`${typeof this.value === "string" ? this.value : ""}`;
    }

    static override styles = css`
        :host {
            display: block;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }
    `;
}

export default UaAnalyticsActionNameCellElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-action-name-cell": UaAnalyticsActionNameCellElement;
    }
}
