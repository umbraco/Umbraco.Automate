import type { UaWorkspaceTreeItemModel } from "../../../../../workspace-management/tree/types.js";
import type { UmbDefaultCollectionContext } from "@umbraco-cms/backoffice/collection";
import { UMB_COLLECTION_CONTEXT } from "@umbraco-cms/backoffice/collection";
import type { UmbTableColumn, UmbTableConfig, UmbTableItem } from "@umbraco-cms/backoffice/components";
import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../../../constants.js";
import { UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN } from "../../../../workspace/automation/paths.js";

const elementName = "ua-automation-tree-item-table-collection-view";

@customElement(elementName)
export class UaAutomationTreeItemTableCollectionViewElement extends UmbLitElement {
    @state()
    private _tableConfig: UmbTableConfig = {
        allowSelection: false,
    };

    @state()
    private _tableColumns: Array<UmbTableColumn> = [
        { name: "Name", alias: "name" },
        { name: "Type", alias: "type" },
        { name: "", alias: "entityActions", align: "right", width: "1%" },
    ];

    @state()
    private _tableItems: Array<UmbTableItem> = [];

    #collectionContext?: UmbDefaultCollectionContext<UaWorkspaceTreeItemModel>;

    constructor() {
        super();
        this.consumeContext(UMB_COLLECTION_CONTEXT, (instance) => {
            this.#collectionContext = instance as UmbDefaultCollectionContext<UaWorkspaceTreeItemModel>;
            this.#observeCollectionItems();
        });
    }

    #observeCollectionItems() {
        if (!this.#collectionContext) return;
        this.observe(
            this.#collectionContext.items,
            (items) => {
                this._tableItems = items.map((item) => this.#buildTableItem(item));
            },
            "automationTreeItemCollectionItemsObserver",
        );
    }

    #buildTableItem(item: UaWorkspaceTreeItemModel): UmbTableItem {
        const isAutomation = item.entityType === UA_AUTOMATION_ENTITY_TYPE;
        const href = isAutomation
            ? UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique: item.unique! })
            : `/umbraco/section/automate/workspace/${item.entityType}/edit/${item.unique}`;

        return {
            id: item.unique!,
            icon: item.icon ?? (item.isFolder ? "icon-folder" : "icon-mindmap"),
            data: [
                {
                    columnAlias: "name",
                    value: html`<uui-button href=${href} label=${item.name}></uui-button>`,
                },
                {
                    columnAlias: "type",
                    value: item.isFolder ? "Folder" : "Automation",
                },
                {
                    columnAlias: "entityActions",
                    value: html`<umb-entity-actions-table-column-view
                        .value=${{
                            entityType: item.entityType,
                            unique: item.unique,
                            name: item.name,
                        }}
                    ></umb-entity-actions-table-column-view>`,
                },
            ],
        };
    }

    override render() {
        return html`
            <umb-table .config=${this._tableConfig} .columns=${this._tableColumns} .items=${this._tableItems}></umb-table>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: flex;
                flex-direction: column;
            }
        `,
    ];
}

export { UaAutomationTreeItemTableCollectionViewElement as element };

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: UaAutomationTreeItemTableCollectionViewElement;
    }
}
