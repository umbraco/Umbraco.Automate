import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

/**
 * A link-styled button for a runs table cell. It opens the run modal rather than navigating, so it
 * is a button, not an `<a>`. It carries its own styles because `umb-table` renders cell content in
 * its own shadow root, where the table host's styles never reach. The click bubbles to the host.
 */
@customElement("ua-run-link")
export class UaRunLinkElement extends UmbLitElement {
    override render() {
        return html`<button type="button"><slot></slot></button>`;
    }

    static override styles = css`
        button {
            padding: 0;
            border: none;
            background: none;
            font: inherit;
            color: var(--uui-color-interactive);
            text-align: left;
            cursor: pointer;
        }

        button:hover {
            color: var(--uui-color-interactive-emphasis);
            text-decoration: underline;
        }

        button:focus-visible {
            outline: 2px solid var(--uui-color-focus);
            outline-offset: 2px;
            border-radius: var(--uui-border-radius);
        }
    `;
}

export default UaRunLinkElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-link": UaRunLinkElement;
    }
}
