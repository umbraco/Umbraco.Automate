import { customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { ensureSlash } from "@umbraco-cms/backoffice/router";
import type { ManifestMenuItem } from "@umbraco-cms/backoffice/menu";
import { UA_ANALYTICS_WORKSPACE_PATH } from "../paths.js";

// TODO: Drop this element for the default menu item kind once umbraco/Umbraco-CMS#24527 is fixed
// (umb-menu-item-layout only sets `active` on `navigationend`).
/**
 * The default menu item layout only works out "active" on a navigation event, so an item that
 * connects after the page has loaded (this one sits behind the workspaces-exist condition) stays
 * inactive. This renders the same item and also checks the location when it connects, the way
 * tree menu items do.
 *
 * This copy leaves out the default element's UmbEntityContext provider and
 * umb-entity-actions-bundle, because nothing registers entity actions for `ua:analytics`.
 */
@customElement("ua-analytics-menu-item")
export class UaAnalyticsMenuItemElement extends UmbLitElement {
    @property({ type: Object, attribute: false })
    manifest?: ManifestMenuItem;

    @state()
    private _isActive = false;

    readonly #href = UA_ANALYTICS_WORKSPACE_PATH;

    override connectedCallback() {
        super.connectedCallback();
        window.addEventListener("navigationend", this.#checkIsActive);
        this.#checkIsActive();
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        window.removeEventListener("navigationend", this.#checkIsActive);
    }

    #checkIsActive = () => {
        // Trailing slashes stop /path-1 matching /path-1-2, and the view segment after the item path still matches.
        this._isActive = ensureSlash(window.location.pathname).includes(ensureSlash(this.#href));
    };

    override render() {
        const label = this.localize.string(this.manifest?.meta.label ?? this.manifest?.name ?? "");
        return html`
            <uui-menu-item label=${label} href=${this.#href} ?active=${this._isActive}>
                <umb-icon slot="icon" name=${this.manifest?.meta.icon ?? ""}></umb-icon>
            </uui-menu-item>
        `;
    }
}

export { UaAnalyticsMenuItemElement as element };

declare global {
    interface HTMLElementTagNameMap {
        "ua-analytics-menu-item": UaAnalyticsMenuItemElement;
    }
}
