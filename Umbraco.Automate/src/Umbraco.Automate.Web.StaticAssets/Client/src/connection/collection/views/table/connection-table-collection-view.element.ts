import { html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
    UmbTableColumn,
    UmbTableItem,
    UmbTableConfig,
    UmbTableSelectedEvent,
    UmbTableDeselectedEvent,
    UmbTableElement,
} from "@umbraco-cms/backoffice/components";
import type { UmbDefaultCollectionContext } from "@umbraco-cms/backoffice/collection";
import { UMB_COLLECTION_CONTEXT } from "@umbraco-cms/backoffice/collection";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { formatDateTime } from "../../../../core/index.js";
import type { UaConnectionItemModel } from "../../../types.js";
import { UA_CONNECTION_ICON } from "../../../constants.js";
import { UA_EDIT_CONNECTION_WORKSPACE_PATH_PATTERN } from "../../../workspace/connection/paths.js";

@customElement("ua-connection-table-collection-view")
export class UaConnectionTableCollectionViewElement extends UmbLitElement {
    @state()
    private _tableConfig: UmbTableConfig = {
        allowSelection: true,
    };

    @state()
    private _items: UmbTableItem[] = [];

    @state()
    private _selection: Array<string> = [];

    #collectionContext?: UmbDefaultCollectionContext<UaConnectionItemModel>;

    private _columns: UmbTableColumn[] = [
        { name: "Name", alias: "name" },
        { name: "Alias", alias: "alias" },
        { name: "Type", alias: "type" },
        { name: "Date Created", alias: "dateCreated" },
    ];

    constructor() {
        super();
        this.consumeContext(UMB_COLLECTION_CONTEXT, (instance) => {
            this.#collectionContext = instance;
            this.#collectionContext?.selection.setSelectable(true);
            this.#observeCollectionItems();
        });
    }

    #observeCollectionItems() {
        if (!this.#collectionContext) return;

        this.observe(
            this.#collectionContext.items,
            (items) => this.#createTableItems(items as UaConnectionItemModel[]),
            "umbCollectionItemsObserver",
        );

        this.observe(
            this.#collectionContext.selection.selection,
            (selection) => {
                this._selection = selection as string[];
            },
            "umbCollectionSelectionObserver",
        );
    }

    #createTableItems(items: UaConnectionItemModel[]) {
        this._items = items.map((item) => ({
            id: item.unique,
            icon: UA_CONNECTION_ICON,
            data: [
                {
                    columnAlias: "name",
                    value: html`<a
                        href=${UA_EDIT_CONNECTION_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: item.unique })}
                        >${item.name}</a
                    >`,
                },
                {
                    columnAlias: "alias",
                    value: item.alias
                        ? html`<uui-tag look="secondary" color="default">${item.alias}</uui-tag>`
                        : "",
                },
                {
                    columnAlias: "type",
                    value: item.type,
                },
                {
                    columnAlias: "dateCreated",
                    value: item.dateCreated ? formatDateTime(item.dateCreated) : "-",
                },
            ],
        }));
    }

    #handleSelect(event: UmbTableSelectedEvent) {
        event.stopPropagation();
        const table = event.target as UmbTableElement;
        this.#collectionContext?.selection.setSelection(table.selection);
    }

    #handleDeselect(event: UmbTableDeselectedEvent) {
        event.stopPropagation();
        const table = event.target as UmbTableElement;
        this.#collectionContext?.selection.setSelection(table.selection);
    }

    render() {
        return html`<umb-table
            .config=${this._tableConfig}
            .columns=${this._columns}
            .items=${this._items}
            .selection=${this._selection}
            @selected=${this.#handleSelect}
            @deselected=${this.#handleDeselect}
        ></umb-table>`;
    }

    static styles = [UmbTextStyles];
}

export default UaConnectionTableCollectionViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-table-collection-view": UaConnectionTableCollectionViewElement;
    }
}
