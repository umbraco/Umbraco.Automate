import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import type { AutomationItemResponseModel } from "../../api/types.gen.js";
import {
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_GROUP_ENTITY_TYPE,
    UA_AUTOMATION_ROOT_ENTITY_TYPE,
} from "../entity.js";
import { UA_AUTOMATION_PENDING_CHANGES_FLAG } from "./constants.js";
import type { UaAutomationTreeItemModel } from "./types.js";

export class UaAutomationTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaAutomationTreeItemModel>
{
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const { data, error } = await tryExecute(
            this,
            AutomationsService.getAutomations({
                query: { skip, take },
            }),
        );

        if (error || !data) {
            return { error };
        }

        return {
            data: {
                total: data.total,
                items: data.items.map((item) => this.#mapAutomationItem(item, null, UA_AUTOMATION_ROOT_ENTITY_TYPE)),
            },
        };
    }

    async getChildrenOf(args: UmbTreeChildrenOfRequestArgs) {
        if (!args.parent?.unique) {
            return {
                data: {
                    total: 0,
                    items: [] as UaAutomationTreeItemModel[],
                },
            };
        }

        // Group children: fetch automations in this group
        if (args.parent.entityType === UA_AUTOMATION_GROUP_ENTITY_TYPE) {
            return this.#getGroupChildren(args.parent.unique);
        }

        return {
            data: {
                total: 0,
                items: [] as UaAutomationTreeItemModel[],
            },
        };
    }

    async #getGroupChildren(groupId: string): Promise<ReturnType<UmbTreeDataSource<UaAutomationTreeItemModel>["getChildrenOf"]>> {
        const { data, error } = await tryExecute(
            this,
            AutomationsService.getAutomations({
                query: { skip: 0, take: 1000, groupId },
            }),
        );

        if (error || !data) {
            return { error };
        }

        return {
            data: {
                total: data.total,
                items: data.items.map((a) => this.#mapAutomationItem(a, groupId, UA_AUTOMATION_GROUP_ENTITY_TYPE)),
            },
        };
    }

    async getAncestorsOf(args: UmbTreeAncestorsOfRequestArgs) {
        if (!args.treeItem.unique) return { data: [] as UaAutomationTreeItemModel[] };

        // Group nodes: no ancestor chain for now
        if (args.treeItem.entityType === UA_AUTOMATION_GROUP_ENTITY_TYPE) {
            return { data: [] as UaAutomationTreeItemModel[] };
        }

        // Automation nodes: if in a group, the group is the ancestor
        // For now, return empty — can be enhanced for breadcrumbs later
        return { data: [] as UaAutomationTreeItemModel[] };
    }

    #mapAutomationItem(
        item: AutomationItemResponseModel,
        parentUnique: string | null,
        parentEntityType: string,
    ): UaAutomationTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            parent: { unique: parentUnique, entityType: parentEntityType },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-mindmap",
            status: item.status,
            triggerAlias: item.triggerAlias ?? null,
            flags: buildFlags(item),
        };
    }
}

function buildFlags(item: AutomationItemResponseModel): Array<{ alias: string }> {
    const flags: Array<{ alias: string }> = [];
    if (hasPendingChanges(item)) flags.push({ alias: UA_AUTOMATION_PENDING_CHANGES_FLAG });
    return flags;
}

// A "Published" automation has pending changes when the draft has been edited past the
// last published snapshot. Drafts and never-published automations don't qualify — the
// "draft" attribute already covers them visually.
function hasPendingChanges(item: AutomationItemResponseModel): boolean {
    return item.status === "Published"
        && item.publishedVersion != null
        && item.version > item.publishedVersion;
}
