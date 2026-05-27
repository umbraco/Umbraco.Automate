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
import type { UaAutomationItemModel } from "../../../types.js";
import { UA_AUTOMATION_ICON } from "../../../constants.js";
import { UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../../../workspace/automation/paths.js";

@customElement("ua-automation-table-collection-view")
export class UaAutomationTableCollectionViewElement extends UmbLitElement {
    @state()
    private _tableConfig: UmbTableConfig = {
        allowSelection: true,
    };

    @state()
    private _items: UmbTableItem[] = [];

    @state()
    private _selection: Array<string> = [];

    #collectionContext?: UmbDefaultCollectionContext<UaAutomationItemModel>;

    private _columns: UmbTableColumn[] = [
        { name: "Name", alias: "name" },
        { name: "Status", alias: "status" },
        { name: "Health", alias: "health" },
        { name: "Modified", alias: "dateModified" },
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
            (items) => this.#createTableItems(items as UaAutomationItemModel[]),
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

    #statusColor(status: string): string {
        switch (status) {
            case "Published":
                return "positive";
            case "Draft":
                return "warning";
            case "Unpublished":
                return "danger";
            default:
                return "default";
        }
    }

    #healthColor(health: string): string {
        switch (health) {
            case "Degraded":
                return "warning";
            case "Disabled":
                return "danger";
            default:
                return "positive";
        }
    }

    #createTableItems(items: UaAutomationItemModel[]) {
        this._items = items.map((item) => ({
            id: item.unique,
            icon: UA_AUTOMATION_ICON,
            data: [
                {
                    columnAlias: "name",
                    value: html`<a
                        href=${UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: item.unique })}
                        >${item.name}</a
                    >`,
                },
                {
                    columnAlias: "status",
                    value: html`<uui-tag color=${this.#statusColor(item.status)} look="secondary">
                        ${item.status}
                    </uui-tag>`,
                },
                {
                    columnAlias: "health",
                    // Only flag unhealthy automations; healthy ones show nothing to avoid noise.
                    value:
                        item.health && item.health !== "Healthy"
                            ? html`<uui-tag color=${this.#healthColor(item.health)} look="secondary">
                                  ${item.health}
                              </uui-tag>`
                            : html`<span>-</span>`,
                },
                {
                    columnAlias: "dateModified",
                    value: item.dateModified ? formatDateTime(item.dateModified) : "-",
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

export default UaAutomationTableCollectionViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-table-collection-view": UaAutomationTableCollectionViewElement;
    }
}
