import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbDetailDataSource } from "@umbraco-cms/backoffice/repository";
import type { UmbFolderModel } from "@umbraco-cms/backoffice/tree";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService, WorkspacesService } from "../../../api/sdk.gen.js";
import { UA_AUTOMATION_GROUP_ENTITY_TYPE } from "../../entity.js";

/**
 * Server data source for automation group folders.
 *
 * Groups are scoped to workspaces in the API (`/workspaces/{id}/groups`).
 * When creating, the parentUnique tells us either:
 *  - A workspace ID (parent entity type is workspace) → create at root of that workspace
 *  - A group ID (parent entity type is group) → create as a sub-folder
 *
 * To resolve the workspaceId when operating on an existing group, we search
 * across workspaces since the group-by-id endpoint requires the workspace path param.
 */
export class UaAutomationFolderServerDataSource extends UmbControllerBase implements UmbDetailDataSource<UmbFolderModel> {
    constructor(host: UmbControllerHost) {
        super(host);
    }

    async createScaffold(preset?: Partial<UmbFolderModel>) {
        return {
            data: {
                entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
                unique: "",
                name: preset?.name ?? "",
            },
        };
    }

    async read(unique: string) {
        const resolved = await this.#resolveGroupWithWorkspace(unique);
        if (!resolved) {
            return { error: undefined, data: undefined };
        }

        return {
            data: {
                entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
                unique: resolved.group.id,
                name: resolved.group.name,
            },
        };
    }

    async create(model: UmbFolderModel, parentUnique: string | null) {
        if (!parentUnique) {
            return { error: new Error("A parent is required to create a folder.") as any };
        }

        // Determine if parentUnique is a workspace or a group.
        // Try workspace first — if the API returns groups, it's a workspace.
        // If not, resolve the group's workspace.
        let workspaceId: string;
        let parentGroupId: string | undefined;

        // First, try treating parentUnique as a workspace ID
        const wsResult = await tryExecute(
            this,
            WorkspacesService.getWorkspacesById({ path: { id: parentUnique } }),
        );

        if (wsResult.data) {
            // Parent is a workspace
            workspaceId = parentUnique;
        } else {
            // Parent is a group — resolve its workspace
            const resolved = await this.#resolveGroupWithWorkspace(parentUnique);
            if (!resolved) {
                return { error: new Error("Could not resolve workspace for parent group.") as any };
            }
            workspaceId = resolved.workspaceId;
            parentGroupId = parentUnique;
        }

        const { data, error } = await tryExecute(
            this,
            WorkspacesService.postWorkspacesByIdGroups({
                path: { id: workspaceId },
                body: {
                    name: model.name,
                    parentId: parentGroupId,
                },
            }),
        );

        if (error) {
            return { error };
        }

        // The API returns the created group ID as a plain GUID string body (see CreateWorkspaceGroupController).
        // The SDK types this as `unknown` (201 response with no schema), so we extract the string value.
        const createdId = typeof data === "string" ? data : String(data ?? "");

        return {
            data: {
                entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
                unique: createdId ?? "",
                name: model.name,
            },
        };
    }

    async update(model: UmbFolderModel) {
        if (!model.unique) {
            return { error: new Error("A unique identifier is required to update a folder.") as any };
        }

        const resolved = await this.#resolveGroupWithWorkspace(model.unique);
        if (!resolved) {
            return { error: new Error("Could not resolve workspace for group.") as any };
        }

        const { error } = await tryExecute(
            this,
            WorkspacesService.putWorkspacesByIdGroupsByGroupId({
                path: { id: resolved.workspaceId, groupId: model.unique },
                body: {
                    name: model.name,
                    parentId: resolved.group.parentId ?? undefined,
                },
            }),
        );

        if (error) {
            return { error };
        }

        // The update API returns no body — return the input model data.
        return {
            data: {
                entityType: UA_AUTOMATION_GROUP_ENTITY_TYPE,
                unique: model.unique,
                name: model.name,
            },
        };
    }

    async delete(unique: string) {
        const resolved = await this.#resolveGroupWithWorkspace(unique);
        if (!resolved) {
            return { error: new Error("Could not resolve workspace for group.") as any };
        }

        return tryExecute(
            this,
            WorkspacesService.deleteWorkspacesByIdGroupsByGroupId({
                path: { id: resolved.workspaceId, groupId: unique },
            }),
        );
    }

    /**
     * Resolves a group's workspace via the group resolve endpoint.
     * Returns the workspaceId and the group data, or undefined if not found.
     */
    async #resolveGroupWithWorkspace(
        groupId: string,
    ): Promise<{ workspaceId: string; group: { id: string; name: string; parentId?: string | null } } | undefined> {
        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsGroupsByGroupId({ path: { groupId } }),
        );
        if (!data) return undefined;
        return { workspaceId: data.workspaceId, group: data };
    }
}
