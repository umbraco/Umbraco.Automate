import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbDetailDataSource } from "@umbraco-cms/backoffice/repository";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { ConnectionsService } from "../../../api/sdk.gen.js";
import { UaConnectionTypeMapper } from "../../type-mapper.js";
import type { UaConnectionDetailModel } from "../../types.js";
import { UA_CONNECTION_ENTITY_TYPE } from "../../constants.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";

export class UaConnectionDetailServerDataSource implements UmbDetailDataSource<UaConnectionDetailModel> {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async createScaffold(preset?: Partial<UaConnectionDetailModel>) {
        const scaffold: UaConnectionDetailModel = {
            unique: UA_EMPTY_GUID,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            alias: "",
            name: "",
            type: "",
            settings: {},
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
            ConnectionsService.getConnectionsById({ path: { id: unique } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaConnectionTypeMapper.toDetailModel(data) };
    }

    async create(model: UaConnectionDetailModel, _parentUnique: string | null) {
        const requestBody = UaConnectionTypeMapper.toCreateRequest(model);

        const { response, error } = await tryExecute(
            this.#host,
            ConnectionsService.postConnections({ body: requestBody }),
        );

        if (error) {
            return { error };
        }

        const locationHeader = response?.headers?.get("Location") ?? "";
        const unique = locationHeader.split("/").pop() ?? "";

        return {
            data: {
                ...model,
                unique,
            },
        };
    }

    async update(model: UaConnectionDetailModel) {
        const requestBody = UaConnectionTypeMapper.toUpdateRequest(model);

        const { error } = await tryExecute(
            this.#host,
            ConnectionsService.putConnectionsById({
                path: { id: model.unique },
                body: requestBody,
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
            ConnectionsService.deleteConnectionsById({ path: { id: unique } }),
        );

        if (error) {
            return { error };
        }

        return {};
    }
}
