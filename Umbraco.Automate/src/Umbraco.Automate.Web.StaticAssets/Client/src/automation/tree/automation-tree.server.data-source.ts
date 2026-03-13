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
import type { AutomationItemResponseModel, WorkspaceItemResponseModel } from "../../api/types.gen.js";
import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_ROOT_ENTITY_TYPE, UA_AUTOMATION_WORKSPACE_ENTITY_TYPE } from "../entity.js";
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
                    items: [] as UaAutomationTreeItemModel[],
                },
            };
        }

        const workspaceId = args.parent.unique;

        // Fetch all automations — API handles workspace scoping server-side.
        // Client-side filter by workspaceId to group under the correct parent.
        const { data, error } = await tryExecute(
            this,
            AutomationsService.getAutomations({
                query: { skip: 0, take: 1000 },
            }),
        );

        if (error || !data) {
            return { error };
        }

        const filtered = data.items.filter((item) => item.workspaceId === workspaceId);

        return {
            data: {
                total: filtered.length,
                items: filtered.map((item) => this.#mapAutomationItem(item, workspaceId)),
            },
        };
    }

    async getAncestorsOf(args: UmbTreeAncestorsOfRequestArgs) {
        if (!args.treeItem.unique) return { data: [] as UaAutomationTreeItemModel[] };

        // Workspace nodes sit directly under the root — no ancestors needed.
        if (args.treeItem.entityType === UA_AUTOMATION_WORKSPACE_ENTITY_TYPE) {
            return { data: [] as UaAutomationTreeItemModel[] };
        }

        // For automation nodes, the ancestor is the parent workspace.
        const { data: automation, error } = await tryExecute(
            this,
            AutomationsService.getAutomationsById({ path: { id: args.treeItem.unique } }),
        );

        if (error || !automation?.workspaceId) {
            return { error, data: [] as UaAutomationTreeItemModel[] };
        }

        const { data: workspace, error: wsError } = await tryExecute(
            this,
            WorkspacesService.getWorkspacesById({ path: { id: automation.workspaceId } }),
        );

        if (wsError || !workspace) {
            return { error: wsError, data: [] as UaAutomationTreeItemModel[] };
        }

        return {
            data: [this.#mapWorkspaceItem(workspace)],
        };
    }

    #mapWorkspaceItem(item: Pick<WorkspaceItemResponseModel, "id" | "name">): UaAutomationTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_AUTOMATION_WORKSPACE_ENTITY_TYPE,
            parent: { unique: null, entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE },
            name: item.name,
            hasChildren: true,
            isFolder: true,
            icon: "icon-folder",
        };
    }

    #mapAutomationItem(item: AutomationItemResponseModel, workspaceId: string): UaAutomationTreeItemModel {
        return {
            unique: item.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            parent: { unique: workspaceId, entityType: UA_AUTOMATION_WORKSPACE_ENTITY_TYPE },
            name: item.name,
            hasChildren: false,
            isFolder: false,
            icon: "icon-mindmap",
            status: item.status,
            isEnabled: item.isEnabled,
        };
    }
}
