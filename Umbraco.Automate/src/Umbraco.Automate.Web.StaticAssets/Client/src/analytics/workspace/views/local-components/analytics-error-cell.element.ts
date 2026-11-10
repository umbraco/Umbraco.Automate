import type { UauiTableCellElement, UauiTableColumn } from "@umbraco/analytics-ui";
import { css, html, customElement, nothing, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/** Only Authentication is tagged: it is the category an editor can fix themselves (expired credentials), as the mockup shows. */
const TAGGED_CATEGORIES: ReadonlySet<string> = new Set(["Authentication"]);

/** The most common error of an action: plain text, except an actionable category, which is a danger tag. */
@customElement("ua-analytics-error-cell")
export class UaAnalyticsErrorCellElement extends UmbLitElement implements UauiTableCellElement {
    @property({ attribute: false })
    column!: UauiTableColumn;

    @property({ attribute: false })
    item!: Record<string, unknown>;

    @property({ attribute: false })
    value: unknown;

    override render() {
        if (typeof this.value !== "string") return nothing;

        const category = this.item?.topErrorCategory;
        if (typeof category === "string" && TAGGED_CATEGORIES.has(category)) {
            return html`<uui-tag size="s" look="secondary" color="danger">${this.value}</uui-tag>`;
        }

        return html`<span class=${category == null ? "none" : ""}>${this.value}</span>`;
    }

    static override styles = css`
        :host {
            display: flex;
            align-items: center;
        }

        .none {
            color: var(--uui-color-text-alt);
        }
    `;
}

export default UaAnalyticsErrorCellElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-error-cell": UaAnalyticsErrorCellElement;
    }
}
