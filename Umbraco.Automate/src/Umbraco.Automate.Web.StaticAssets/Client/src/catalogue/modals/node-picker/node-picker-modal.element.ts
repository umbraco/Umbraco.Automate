import { css, html, customElement, state, repeat, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UaCatalogueRepository } from "../../repository/catalogue.repository.js";
import type { UaCatalogueItemModel } from "../../types.js";
import type { UaNodePickerModalData, UaNodePickerModalValue } from "./types.js";

interface CatalogueGroup {
    name: string;
    items: UaCatalogueItemModel[];
}

@customElement("ua-node-picker-modal")
export class UaNodePickerModalElement extends UmbModalBaseElement<UaNodePickerModalData, UaNodePickerModalValue> {
    @state()
    private _groups: CatalogueGroup[] = [];

    @state()
    private _filteredGroups: CatalogueGroup[] = [];

    @state()
    private _search = "";

    @state()
    private _loading = true;

    @state()
    private _error = false;

    #repository?: UaCatalogueRepository;

    override connectedCallback() {
        super.connectedCallback();
        this.#repository = new UaCatalogueRepository(this);
        this.#loadItems();
    }

    async #loadItems() {
        this._loading = true;
        this._error = false;

        const mode = this.data?.mode ?? "action";
        const result = mode === "trigger"
            ? await this.#repository!.requestTriggers()
            : await this.#repository!.requestActions();

        if (result.error || !result.data) {
            this._error = true;
            this._loading = false;
            return;
        }

        this._groups = this.#groupItems(result.data);
        this._filteredGroups = this._groups;
        this._loading = false;
    }

    #groupItems(items: UaCatalogueItemModel[]): CatalogueGroup[] {
        const map = new Map<string, UaCatalogueItemModel[]>();

        for (const item of items) {
            const groupName = item.group || "General";
            const group = map.get(groupName);
            if (group) {
                group.push(item);
            } else {
                map.set(groupName, [item]);
            }
        }

        return Array.from(map.entries())
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([name, groupItems]) => ({
                name,
                items: groupItems.sort((a, b) => a.name.localeCompare(b.name)),
            }));
    }

    #onSearchInput(event: InputEvent) {
        const target = event.target as HTMLInputElement;
        this._search = target.value;
        this.#applyFilter();
    }

    #applyFilter() {
        const query = this._search.toLowerCase().trim();
        if (!query) {
            this._filteredGroups = this._groups;
            return;
        }

        this._filteredGroups = this._groups
            .map((group) => ({
                name: group.name,
                items: group.items.filter(
                    (item) =>
                        item.name.toLowerCase().includes(query) ||
                        (item.description?.toLowerCase().includes(query) ?? false) ||
                        item.alias.toLowerCase().includes(query),
                ),
            }))
            .filter((group) => group.items.length > 0);
    }

    #onSelect(item: UaCatalogueItemModel) {
        this.value = { item };
        this.modalContext?.submit();
    }

    #onClose() {
        this.modalContext?.reject();
    }

    #getTitle(): string {
        const mode = this.data?.mode ?? "action";
        return mode === "trigger"
            ? this.localize.term("uaCatalogue_selectTrigger")
            : this.localize.term("uaCatalogue_selectAction");
    }

    override render() {
        return html`
            <umb-body-layout .headline=${this.#getTitle()}>
                <div id="main">
                    <uui-input
                        id="search"
                        type="search"
                        placeholder=${this.localize.term("uaCatalogue_searchPlaceholder")}
                        @input=${this.#onSearchInput}
                        .value=${this._search}
                        label="Search"
                    >
                        <uui-icon name="icon-search" slot="prepend"></uui-icon>
                    </uui-input>

                    ${when(this._loading, () => html`<div id="loader"><uui-loader></uui-loader></div>`)}
                    ${when(this._error, () => html`<p class="error">${this.localize.term("uaCatalogue_loadError")}</p>`)}
                    ${when(
                        !this._loading && !this._error && this._filteredGroups.length === 0,
                        () => html`<p class="empty">${this.localize.term("uaCatalogue_noResults")}</p>`,
                    )}
                    ${when(
                        !this._loading && !this._error,
                        () => html`
                            ${repeat(
                                this._filteredGroups,
                                (group) => group.name,
                                (group) => this.#renderGroup(group),
                            )}
                        `,
                    )}
                </div>

                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onClose}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    #renderGroup(group: CatalogueGroup) {
        return html`
            <uui-box .headline=${group.name}>
                <uui-ref-list>
                    ${repeat(
                        group.items,
                        (item) => item.alias,
                        (item) => this.#renderItem(item),
                    )}
                </uui-ref-list>
            </uui-box>
        `;
    }

    #renderItem(item: UaCatalogueItemModel) {
        return html`
            <uui-ref-node
                name=${item.name}
                detail=${item.description ?? ""}
                @open=${() => this.#onSelect(item)}
            >
                <umb-icon slot="icon" name=${item.icon || "icon-plugin"}></umb-icon>
            </uui-ref-node>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            #main {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-4);
                height: 100%;
            }

            #search {
                width: 100%;
            }

            #search uui-icon {
                padding-left: var(--uui-size-space-3);
            }

            #loader {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-1);
                min-height: 200px;
            }

            .error {
                color: var(--uui-color-danger);
                text-align: center;
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
            }
        `,
    ];
}

export default UaNodePickerModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-node-picker-modal": UaNodePickerModalElement;
    }
}
