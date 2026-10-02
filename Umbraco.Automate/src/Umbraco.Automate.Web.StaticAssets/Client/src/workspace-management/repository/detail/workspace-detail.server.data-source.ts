import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbDetailDataSource } from "@umbraco-cms/backoffice/repository";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { WorkspacesService } from "../../../api/sdk.gen.js";
import { UaWorkspaceTypeMapper } from "../../type-mapper.js";
import type { UaWorkspaceDetailModel } from "../../types.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";

export class UaWorkspaceDetailServerDataSource implements UmbDetailDataSource<UaWorkspaceDetailModel> {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async createScaffold(preset?: Partial<UaWorkspaceDetailModel>) {
        const scaffold: UaWorkspaceDetailModel = {
            unique: UA_EMPTY_GUID,
            entityType: UA_WORKSPACE_ENTITY_TYPE,
            alias: "",
            name: "",
            serviceAccountKey: UA_EMPTY_GUID,
            userGroups: [],
            allowedConnections: [],
            version: 0,
            dateCreated: new Date().toISOString(),
            dateModified: new Date().toISOString(),
            ...preset,
        };

        return { data: scaffold };
    }

    async read(unique: string) {
        const { data, error } = await tryExecute(
            this.#host,
            WorkspacesService.getWorkspacesById({ path: { id: unique } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaWorkspaceTypeMapper.toDetailModel(data) };
    }

    async create(model: UaWorkspaceDetailModel, _parentUnique: string | null) {
        const requestBody = UaWorkspaceTypeMapper.toCreateRequest(model);

        // throwOnError: tryExecute only notifies (toast) on a rejected promise, and the generated SDK
        // client resolves 4xx/5xx by default, so a failed save would otherwise report nothing.
        const { response, error } = await tryExecute(
            this.#host,
            WorkspacesService.postWorkspaces({ body: requestBody, throwOnError: true }),
        );

        if (error) {
            return { error };
        }

        const locationHeader = response?.headers?.get("Location") ?? "";
        const unique = locationHeader.split("/").pop() ?? "";

        return this.read(unique);
    }

    async update(model: UaWorkspaceDetailModel) {
        const requestBody = UaWorkspaceTypeMapper.toUpdateRequest(model);

        const { error } = await tryExecute(
            this.#host,
            // throwOnError: see create().
            WorkspacesService.putWorkspacesById({
                path: { id: model.unique },
                body: requestBody,
                throwOnError: true,
            }),
        );

        if (error) {
            return { error };
        }

        return this.read(model.unique);
    }

    async delete(unique: string) {
        const { error } = await tryExecute(
            this.#host,
            WorkspacesService.deleteWorkspacesById({ path: { id: unique } }),
        );

        if (error) {
            return { error };
        }

        return {};
    }
}
