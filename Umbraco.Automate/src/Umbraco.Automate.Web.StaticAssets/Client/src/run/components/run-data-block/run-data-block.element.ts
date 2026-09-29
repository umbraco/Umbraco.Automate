import { css, html, customElement, property, state, nothing, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaRunDataValueModel } from "../../types.js";

type JsonObject = { [key: string]: unknown };

function isJsonObject(value: unknown): value is JsonObject {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}

function tryParseJson(text: string): { ok: true; value: unknown } | { ok: false } {
    try {
        return { ok: true, value: JSON.parse(text) };
    } catch {
        return { ok: false };
    }
}

/** Turns a property key into a readable label: `statusCode` / `status_code` → "Status code". */
function humanizeKey(key: string): string {
    const words = key
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[_-]+/g, " ")
        .trim()
        .toLowerCase();
    return words.charAt(0).toUpperCase() + words.slice(1);
}

/**
 * Renders one recorded run payload — a step's input (its resolved settings) or output, or a run's
 * trigger data. These are stored as JSON because they are serialized objects, not because the
 * content is JSON, so an object is shown as a list of its properties; nested objects and lists,
 * and text values that are themselves JSON (a webhook body, say), are shown formatted. Anything
 * else — including a value the server truncated, which is no longer valid JSON — is shown as
 * stored, as is everything when "Raw JSON" is switched on. The server has already masked
 * sensitive values; long values scroll within the block.
 */
@customElement("ua-run-data-block")
export class UaRunDataBlockElement extends UmbLitElement {
    /** Rendered above the value. Leave empty when the surrounding UI already names it. */
    @property()
    label = "";

    /** Text shown when nothing was recorded. */
    @property({ attribute: "empty-text" })
    emptyText = "";

    @property({ attribute: false })
    data?: UaRunDataValueModel;

    @state()
    private _raw = false;

    /** The value as an object to list, or undefined when it has to be shown as stored. */
    #properties(): JsonObject | undefined {
        if (!this.data?.value || this.data.truncated) return undefined;

        const parsed = tryParseJson(this.data.value);
        return parsed.ok && isJsonObject(parsed.value) ? parsed.value : undefined;
    }

    #renderPropertyValue(value: unknown) {
        if (value === null || value === undefined) {
            return html`<span class="null">null</span>`;
        }

        if (typeof value === "string") {
            const parsed = tryParseJson(value);
            if (parsed.ok && typeof parsed.value === "object" && parsed.value !== null) {
                return html`<pre class="json">${JSON.stringify(parsed.value, null, 2)}</pre>`;
            }

            return html`<span class="text">${value}</span>`;
        }

        if (typeof value === "object") {
            return html`<pre class="json">${JSON.stringify(value, null, 2)}</pre>`;
        }

        return html`<span class="text">${String(value)}</span>`;
    }

    #renderProperties(properties: JsonObject) {
        return html`
            <dl class="properties">
                ${repeat(
                    Object.entries(properties),
                    ([key]) => key,
                    ([key, value]) => html`
                        <dt title=${key}>${humanizeKey(key)}</dt>
                        <dd>${this.#renderPropertyValue(value)}</dd>
                    `,
                )}
            </dl>
        `;
    }

    #renderValue() {
        if (this.data?.value == null) {
            return html`<p class="empty">${this.emptyText}</p>`;
        }

        const properties = this.#properties();
        if (properties && Object.keys(properties).length === 0) {
            return html`<p class="empty">${this.emptyText}</p>`;
        }

        return html`
            ${properties
                ? html`
                      <div class="toolbar">
                          <uui-toggle
                              label=${this.localize.term("uaRun_rawJson")}
                              ?checked=${this._raw}
                              @change=${() => (this._raw = !this._raw)}
                          ></uui-toggle>
                      </div>
                  `
                : nothing}
            <div class="data-output">
                ${properties && !this._raw
                    ? this.#renderProperties(properties)
                    : html`<pre class="json">${this.data.value}</pre>`}
            </div>
            ${this.data.truncated
                ? html`
                      <p class="truncated">
                          <uui-icon name="icon-alert"></uui-icon>
                          ${this.localize.term("uaRun_dataTruncated")}
                      </p>
                  `
                : nothing}
        `;
    }

    override render() {
        // Without a label (e.g. inside the trigger row or a step's tab, which already name it)
        // the property layout would still reserve an empty header row above the value, so skip it.
        if (!this.label) {
            return this.#renderValue();
        }

        return html`
            <umb-property-layout label=${this.label} orientation="vertical">
                <div slot="editor">${this.#renderValue()}</div>
            </umb-property-layout>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            umb-property-layout[orientation="vertical"] {
                padding-top: 0;
                padding-bottom: 0;
            }

            .toolbar {
                display: flex;
                justify-content: flex-end;
                margin-bottom: var(--uui-size-space-2);
                font-size: var(--uui-size-4);
            }

            .data-output {
                background: var(--uui-color-surface-alt);
                border: 1px solid var(--uui-color-border);
                border-radius: var(--uui-border-radius);
                padding: var(--uui-size-space-3);
                max-height: 20rem;
                overflow: auto;
            }

            .properties {
                display: grid;
                grid-template-columns: minmax(8rem, max-content) 1fr;
                gap: var(--uui-size-space-2) var(--uui-size-space-5);
                margin: 0;
            }

            .properties dt {
                font-weight: 700;
            }

            .properties dd {
                margin: 0;
                min-width: 0;
            }

            .json {
                font-family: monospace;
                font-size: var(--uui-size-4);
                white-space: pre-wrap;
                overflow-wrap: anywhere;
                margin: 0;
            }

            .text {
                white-space: pre-wrap;
                overflow-wrap: anywhere;
            }

            .null {
                color: var(--uui-color-text-alt);
                font-style: italic;
            }

            .empty {
                color: var(--uui-color-text-alt);
                font-style: italic;
                margin: 0;
            }

            .truncated {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
                color: var(--uui-color-warning-standalone);
                font-size: var(--uui-size-4);
                margin: var(--uui-size-space-2) 0 0;
            }
        `,
    ];
}

export default UaRunDataBlockElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-data-block": UaRunDataBlockElement;
    }
}
