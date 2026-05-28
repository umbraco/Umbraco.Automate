import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
    UmbTreeDataSource,
    UmbTreeRootItemsRequestArgs,
    UmbTreeChildrenOfRequestArgs,
    UmbTreeAncestorsOfRequestArgs,
} from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService, WorkspacesService } from "../../api/sdk.gen.js";
import type { AutomationAncestorResponseModel, AutomationItemResponseModel, WorkspaceItemResponseModel, WorkspaceGroupResponseModel } from "../../api/types.gen.js";
import { UA_WORKSPACE_ENTITY_TYPE, UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../entity.js";
import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../../automation/entity.js";
import { UA_AUTOMATION_PENDING_CHANGES_FLAG } from "../../automation/tree/constants.js";
import type { UaWorkspaceTreeItemModel } from "./types.js";

export class UaWorkspaceTreeServerDataSource
    extends UmbControllerBase
    implements UmbTreeDataSource<UaWorkspaceTreeItemModel>
{
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async getRootItems(args: UmbTreeRootItemsRequestArgs) {
        const skip = args.paging && "skip" in args.paging ? args.paging.skip : 0;
        const take = args.paging && "take" in args.paging ? args.paging.take : 100;

        const { data, error } = await tryExecute(
            this,
            WorkspacesService.getWorkspaces({
                query: { skip, take },
            }),
        );

        if (error || !data) {
            return { error };
        }

        return {
            data: {
                total: data.total,
                items: data.items.map((item) => this.#mapWorkspaceItem(item)),
            },
        };
    }

    async getChildrenOf(args: UmbTreeChildrenOfRequestArgs) {
        if (!args.parent?.unique) {
            return {
                data: {
                    total: 0,
                    items: [] as UaWorkspaceTreeItemModel[],
                },
            };
        }

        // Workspace children: root-level groups + ungrouped automations
        if (args.parent.entityType === UA_WORKSPACE_ENTITY_TYPE) {
            return this.#getWorkspaceChildren(args.parent.unique);
        }

        // Group children: sub-groups + automations in this group
        if (args.parent.entityType === UA_AUTOMATION_GROUP_ENTITY_TYPE) {
            return this.#getGroupChildren(args.parent.unique);
        }

        return {
            data: {
                total: 0,
                items: [] as UaWorkspaceTreeItemModel[],
            },
        };
    }

    async #getWorkspaceChildren(workspaceId: string): Promise<ReturnType<UmbTreeDataSource<UaWorkspaceTreeItemModel>["getChildrenOf"]>> {
        // Fetch root-level groups and ungrouped automations in parallel.
        // groupId = empty GUID → "root level" (automations with no group).
        const [groupsResult, automationsResult] = await Promise.all([
            tryExecute(
                this,
                WorkspacesService.getWorkspacesByIdGroups({
                    path: { id: workspaceId },
                }),
            ),
            tryExecute(
                this,
                AutomationsService.getAutomations({
                    query: { workspaceId, groupId: "00000000-0000-0000-0000-000000000000", skip: 0, take: 1000 },
                }),
            ),
        ]);

        const items: UaWorkspaceTreeItemModel[] = [];
        let total = 0;

        // Add root-level groups for this workspace
        if (groupsResult.data) {
            const rootGroups = groupsResult.data.filter((g) => !g.parentId);
            items.push(...rootGroups.map((g) => this.#mapGroupItem(g, workspaceId, UA_WORKSPACE_ENTITY_TYPE)));
            total += rootGroups.length;
        }

        // Add ungrouped automations for this workspace
        if (automationsResult.data) {
            items.push(...automationsResult.data.items.map((a) => this.#mapAutomationItem(a, workspaceId, UA_WORKSPACE_ENTITY_TYPE)));
            total += automationsResult.data.total;
        }

        return {
            data: {
                total,
                items,
            },
        };
    }

    async #getGroupChildren(groupId: string): Promise<ReturnType<UmbTreeDataSource<UaWorkspaceTreeItemModel>["getChildrenOf"]>> {
        // Resolve the workspace for this group
        const resolved = await this.#resolveWorkspaceForGroup(groupId);
        if (!resolved) {
            return {
                data: {
                    total: 0,
                    items: [] as UaWorkspaceTreeItemModel[],
                },
            };
        }

        // Fetch sub-groups and automations in parallel
        const [subGroupsResult, automationsResult] = await Promise.all([
            tryExecute(
                this,
                WorkspacesService.getWorkspacesByIdGroups({
                    path: { id: resolved.workspaceId },
                    query: { parentGroupId: groupId },
                }),
            ),
            tryExecute(
                this,
                AutomationsService.getAutomations({
                    query: { skip: 0, take: 1000, groupId },
                }),
            ),
        ]);

        const items: UaWorkspaceTreeItemModel[] = [];
        let total = 0;

        if (subGroupsResult.data) {
            items.push(...subGroupsResult.data.map((g) => this.#mapGroupItem(g, groupId, UA_AUTOMATION_GROUP_ENTITY_TYPE)));
            total += subGroupsResult.data.length;
        }

        if (automationsResult.data) {
            items.push(...automationsResult.data.items.map((a) => this.#mapAutomationItem(a, groupId, UA_AUTOMATION_GROUP_ENTITY_TYPE)));
            total += automationsResult.data.total;
        }

        return {
            data: {
                total,
                items,
            },
        };
    }

    async getAncestorsOf(args: UmbTreeAncestorsOfRequestArgs) {
        const { unique, entityType } = args.treeItem;
        if (!unique) return { data: [] as UaWorkspaceTreeItemModel[] };

        if (entityType === UA_AUTOMATION_ENTITY_TYPE) {
            // Use server-side ancestors endpoint
            const { data, error } = await tryExecute(
                this,
                AutomationsService.getAutomationsByIdAncestors({ path: { id: unique } }),
            );
            if (error || !data) return { data: [] as UaWorkspaceTreeItemModel[] };
            return { data: data.map((a) => this.#mapAncestorItem(a)) };
        }

        if (entityType === UA_AUTOMATION_GROUP_ENTITY_TYPE) {
            // For groups, resolve workspace then get ancestors via a group's automation context
            // Groups are children of workspaces, so we need the workspace as ancestor
            const resolved = await this.#resolveWorkspaceForGroup(unique);
            if (!resolved) return { data: [] as UaWorkspaceTreeItemModel[] };

            const ancestors: UaWorkspaceTreeItemModel[] = [];

            // Add the workspace
            const { data: ws } = await tryExecute(
                this,
                WorkspacesService.getWorkspacesById({ path: { id: resolved.workspaceId } }),
            );
            if (ws) ancestors.push(this.#mapWorkspaceItem(ws));

            // Walk up parent group chain (excluding the item itself)
            const { data: allGroups } = await tryExecute(
                this,
                WorkspacesService.getWorkspacesByIdGroups({ path: { id: resolved.workspaceId } }),
            );
            if (allGroups) {
                const currentGroup = allGroups.find((g) => g.id === unique);
                if (currentGroup?.parentId) {
                    const parentChain = this.#buildGroupChain(allGroups, currentGroup.parentId, resolved.workspaceId);
                    ancestors.push(...parentChain);
                }
            }

            return { data: ancestors };
        }

        // Workspace is a root-level item — return itself so the breadcrumb's
        // slice(0, -1) preserves the tree root.
        if (entityType === UA_WORKSPACE_ENTITY_TYPE && unique) {
            const { data: ws } = await tryExecute(
                this,
                WorkspacesService.getWorkspacesById({ path: { id: unique } }),
            );
            if (ws) {
                return { data: [this.#mapWorkspaceItem(ws)] };
            }
        }

        return { data: [] as UaWorkspaceTreeItemModel[] };
    }

    /** Walks from groupId up through parentId, returning ancestors in root-first order. */
    #buildGroupChain(
        allGroups: WorkspaceGroupResponseModel[],
        groupId: string,
        workspaceId: string,
    ): UaWorkspaceTreeItemModel[] {
        const chain: UaWorkspaceTreeItemModel[] = [];
        let currentId: string | null | undefined = groupId;

        while (currentId) {
            const group = allGroups.find((g) => g.id === currentId);
            if (!group) break;

            const parentUnique = group.parentId ?? workspaceId;
            const parentEntityType = group.parentId ? UA_AUTOMATION_GROUP_ENTITY_TYPE : UA_WORKSPACE_ENTITY_TYPE;

            chain.unshift(this.#mapGroupItem(group, parentUnique, parentEntityType));
            currentId = group.parentId;
        }

        return chain;
    }

    #mapAncestorItem(item: AutomationAncestorResponseModel): UaWorkspaceTreeItemModel {
        return {
            unique: item.id,
            entityType: item.entityType as UaWorkspaceTreeItemModel["entityType"],
            parent: {
                unique: item.parentId ?? null,
                entityType: item.parentEntityType ?? UA_WORKSPACE_ROOT_ENTITY_TYPE,
            },
            name: item.name,
            hasChildren: item.hasChildren,
            isFolder: item.isFolder,
            icon: item.isFolder ? "icon-folder" : "icon-users",
        };
    }

    async #resolveWorkspaceForGroup(
        groupId: string,
    ): Promise<{ workspaceId: string } | undefined> {
        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsGroupsByGroupId({ path: { groupId } }),
        );
        if (!data) return undefined;
        return { workspaceId: data.workspaceId };
    }

    #mapWorkspaceItem(item: WorkspaceItemResponseModel): UaWorkspaceTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_WORKSPACE_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: true,
            isFolder: false,
            icon: "icon-users",
        };
    }

    #mapGroupItem(
        item: WorkspaceGroupResponseModel,
        parentUnique: string,
        parentEntityType: string,
    ): UaWorkspaceTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
            parent: { unique: parentUnique, entityType: parentEntityType },
            name: item.name,
            hasChildren: true,
            isFolder: true,
            icon: "icon-folder",
        };
    }

    #mapAutomationItem(
        item: AutomationItemResponseModel,
        parentUnique: string,
        parentEntityType: string,
    ): UaWorkspaceTreeItemModel {
        const flags: Array<{ alias: string }> = [];
        if (item.status === "Published"
            && item.publishedVersion != null
            && item.version > item.publishedVersion) {
            flags.push({ alias: UA_AUTOMATION_PENDING_CHANGES_FLAG });
        }

        return {
            unique: item.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            parent: { unique: parentUnique, entityType: parentEntityType },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-mindmap",
            status: item.status,
            health: item.health,
            triggerAlias: item.triggerAlias ?? null,
            flags,
        };
    }
}
