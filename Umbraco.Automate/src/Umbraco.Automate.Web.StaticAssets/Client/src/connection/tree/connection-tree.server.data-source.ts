import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { ConnectionsService, CatalogueService } from "../../api/sdk.gen.js";
import type { ConnectionItemResponseModel } from "../../api/types.gen.js";
import { UA_CONNECTION_ENTITY_TYPE, UA_CONNECTION_ROOT_ENTITY_TYPE } from "../entity.js";
import type { UaConnectionTreeItemModel } from "./types.js";

const DEFAULT_ICON = "icon-link";

export class UaConnectionTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaConnectionTreeItemModel>
{
    #iconsByType: Map<string, string> | undefined;

    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const [connectionsResult, iconMap] = await Promise.all([
            tryExecute(this, ConnectionsService.getConnections({ query: { skip, take } })),
            this.#getIconMap(),
        ]);

        const { data, error } = connectionsResult;
        if (error || !data) {
            return { error };
        }

        return {
            data: {
                total: data.total,
                items: data.items.map((item) => this.#mapItem(item, iconMap)),
            },
        };
    }

    async getChildrenOf(_args: UmbTreeChildrenOfRequestArgs) {
        // Flat list — no hierarchy.
        return {
            data: {
                total: 0,
                items: [] as UaConnectionTreeItemModel[],
            },
        };
    }

    async getAncestorsOf(args: UmbTreeAncestorsOfRequestArgs) {
        const { unique } = args.treeItem;
        if (!unique) return { data: [] as UaConnectionTreeItemModel[] };

        // Return the entity itself so the breadcrumb's slice(0, -1) preserves the tree root.
        const [{ data: conn }, iconMap] = await Promise.all([
            tryExecute(this, ConnectionsService.getConnectionsById({ path: { id: unique } })),
            this.#getIconMap(),
        ]);
        if (conn) {
            return { data: [this.#mapItem(conn, iconMap)] };
        }

        return { data: [] as UaConnectionTreeItemModel[] };
    }

    async #getIconMap(): Promise<Map<string, string>> {
        if (this.#iconsByType) return this.#iconsByType;

        const { data } = await tryExecute(this, CatalogueService.getCatalogueConnectionTypes());
        this.#iconsByType = new Map(
            (data ?? [])
                .filter((ct) => ct.icon)
                .map((ct) => [ct.alias, ct.icon!]),
        );
        return this.#iconsByType;
    }

    #mapItem(item: ConnectionItemResponseModel, iconMap: Map<string, string>): UaConnectionTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_CONNECTION_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: iconMap.get(item.type) ?? DEFAULT_ICON,
        };
    }
}
