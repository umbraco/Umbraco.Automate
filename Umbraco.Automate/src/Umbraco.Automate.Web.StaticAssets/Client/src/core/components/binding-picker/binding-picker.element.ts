import { css, html, customElement, property, nothing, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { BindingSource } from "../../utils/binding-context.utils.js";

/**
 * Popover tree component that displays available binding sources and their
 * output properties. Clicking a leaf dispatches a `ua:binding-select` event
 * with the full `${...}` expression.
 */
@customElement("ua-binding-picker")
export class UaBindingPickerElement extends UmbLitElement {
    @property({ type: Array })
    sources: BindingSource[] = [];

    @state()
    private _expandedSources = new Set<string>();

    #toggleSource(id: string) {
        if (this._expandedSources.has(id)) {
            this._expandedSources.delete(id);
        } else {
            this._expandedSources.add(id);
        }
        this.requestUpdate();
    }

    #selectLeaf(bindingPrefix: string, path: string) {
        const expression = `\${ ${bindingPrefix}.${path} }`;
        this.dispatchEvent(
            new CustomEvent("ua:binding-select", {
                detail: { expression },
                bubbles: true,
                composed: true,
            }),
        );
    }

    override render() {
        if (this.sources.length === 0) {
            return html`<div class="empty">No binding data available</div>`;
        }

        return html`
            <div class="picker">
                ${repeat(
                    this.sources,
                    (s) => s.id,
                    (source) => this.#renderSource(source),
                )}
            </div>
        `;
    }

    #renderSource(source: BindingSource) {
        const expanded = this._expandedSources.has(source.id);
        return html`
            <div class="source">
                <button
                    class="source__header"
                    type="button"
                    @click=${() => this.#toggleSource(source.id)}
                >
                    <uui-icon name=${source.icon}></uui-icon>
                    <span class="source__name">${source.label}</span>
                    <uui-icon name=${expanded ? "icon-navigation-up" : "icon-navigation-down"}></uui-icon>
                </button>
                ${expanded
                    ? html`<ul class="source__leaves">
                          ${repeat(
                              source.leaves,
                              (l) => l.path,
                              (leaf) => html`
                                  <li>
                                      <button
                                          class="leaf"
                                          type="button"
                                          title="\${ ${source.bindingPrefix}.${leaf.path} }"
                                          @click=${() => this.#selectLeaf(source.bindingPrefix, leaf.path)}
                                      >
                                          <span class="leaf__name">${leaf.label}</span>
                                          <span class="leaf__type">${leaf.type}</span>
                                      </button>
                                  </li>
                              `,
                          )}
                      </ul>`
                    : nothing}
            </div>
        `;
    }

    static override styles = css`
        :host {
            display: block;
            font-size: 13px;
        }

        .picker {
            min-width: 240px;
            max-height: 300px;
            overflow-y: auto;
        }

        .empty {
            padding: 12px;
            color: var(--uui-color-text-alt, #888);
            text-align: center;
        }

        .source__header {
            display: flex;
            align-items: center;
            gap: 6px;
            width: 100%;
            padding: 8px 12px;
            border: none;
            background: var(--uui-color-surface-alt, #f3f3f5);
            cursor: pointer;
            font-weight: 600;
            font-size: 12px;
            color: var(--uui-color-text, #303033);
        }

        .source__header:hover {
            background: var(--uui-color-surface-emphasis, #e9e9eb);
        }

        .source__name {
            flex: 1;
            text-align: left;
        }

        .source__leaves {
            list-style: none;
            margin: 0;
            padding: 0;
        }

        .leaf {
            display: flex;
            align-items: center;
            justify-content: space-between;
            width: 100%;
            padding: 6px 12px 6px 32px;
            border: none;
            background: transparent;
            cursor: pointer;
            font-size: 12px;
            color: var(--uui-color-text, #303033);
        }

        .leaf:hover {
            background: var(--uui-color-selected, #f0f7ff);
        }

        .leaf__name {
            font-family: monospace;
        }

        .leaf__type {
            color: var(--uui-color-text-alt, #888);
            font-size: 11px;
        }
    `;
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-picker": UaBindingPickerElement;
    }
}
