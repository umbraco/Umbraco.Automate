import { css, html, customElement, property, repeat, state, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { BindingSource } from "../../utils/binding-context.utils.js";

/**
 * Picker component that displays available binding sources grouped by origin
 * (trigger, steps, loop). Clicking a leaf dispatches a `ua:binding-select`
 * event with the full `${...}` expression.
 */
@customElement("ua-binding-picker")
export class UaBindingPickerElement extends UmbLitElement {
    @property({ type: Array })
    sources: BindingSource[] = [];

    @state()
    private _search = "";

    #onSearchInput(event: InputEvent) {
        const target = event.target as HTMLInputElement;
        this._search = target.value;
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

    #getFilteredSources(): BindingSource[] {
        const query = this._search.toLowerCase().trim();
        if (!query) return this.sources;

        return this.sources
            .map((source) => ({
                ...source,
                leaves: source.leaves.filter(
                    (leaf) =>
                        leaf.path.toLowerCase().includes(query) ||
                        leaf.label.toLowerCase().includes(query),
                ),
            }))
            .filter((source) => source.leaves.length > 0);
    }

    override render() {
        if (this.sources.length === 0) {
            return html`<p class="empty">${this.localize.term("uaBindings_noData")}</p>`;
        }

        const filtered = this.#getFilteredSources();

        return html`
            <div id="main">
                <uui-input
                    id="search"
                    type="search"
                    placeholder=${this.localize.term("uaBindings_filterPlaceholder")}
                    @input=${this.#onSearchInput}
                    .value=${this._search}
                    label=${this.localize.term("uaLabels_search")}
                >
                    <uui-icon name="icon-search" slot="prepend"></uui-icon>
                </uui-input>

                ${when(
                    filtered.length === 0,
                    () => html`<p class="empty">${this.localize.term("uaBindings_noResults")}</p>`,
                    () => html`${repeat(
                        filtered,
                        (s) => s.id,
                        (source) => this.#renderSource(source),
                    )}`,
                )}
            </div>
        `;
    }

    #renderSource(source: BindingSource) {
        // Show the source's step ID for predecessor steps (GUID-shaped id), so users
        // can match a binding source to its node on the canvas. Skip "trigger",
        // "previous" and "loop" pseudo-sources where the id isn't an entity key.
        const showId = source.bindingPrefix.startsWith("steps.");
        return html`
            <uui-box>
                <div slot="headline" class="source-headline">
                    <span>${source.label}</span>
                    <div class="source-chips">
                        ${source.subLabel
                            ? html`<code class="source-chip" title="Alias">${source.subLabel}</code>`
                            : ""}
                        ${showId
                            ? html`<code class="source-chip source-chip--id" title="Step ID">${source.id}</code>`
                            : ""}
                    </div>
                </div>
                <uui-icon slot="header-actions" name=${source.icon}></uui-icon>
                <uui-ref-list>
                    ${repeat(
                        source.leaves,
                        (l) => l.path,
                        (leaf) => html`
                            <uui-ref-node
                                name=${leaf.path}
                                detail=${leaf.type}
                                @open=${() => this.#selectLeaf(source.bindingPrefix, leaf.path)}
                            >
                                <uui-icon slot="icon" name="icon-code"></uui-icon>
                            </uui-ref-node>
                        `,
                    )}
                </uui-ref-list>
            </uui-box>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            #main {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-4);
            }

            #search {
                width: 100%;
            }

            #search uui-icon {
                padding-left: var(--uui-size-space-3);
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
            }

            .source-headline {
                display: flex;
                flex-direction: column;
                gap: 2px;
                line-height: 1.2;
            }

            .source-chips {
                display: flex;
                flex-wrap: wrap;
                gap: 4px;
            }

            .source-chip {
                font-family: var(--uui-font-monospace, monospace);
                font-size: 11px;
                color: var(--uui-color-text-alt);
                background: var(--uui-color-surface-alt, rgba(0, 0, 0, 0.04));
                padding: 1px 6px;
                border-radius: 3px;
                font-weight: normal;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                max-width: 100%;
            }

            .source-chip--id {
                opacity: 0.7;
            }
        `,
    ];
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-binding-picker": UaBindingPickerElement;
    }
}
