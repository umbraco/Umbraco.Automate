import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbDetailDataSource } from "@umbraco-cms/backoffice/repository";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../../api/sdk.gen.js";
import { UaAutomationTypeMapper } from "../../type-mapper.js";
import type { UaAutomationDetailModel } from "../../types.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";

export class UaAutomationDetailServerDataSource implements UmbDetailDataSource<UaAutomationDetailModel> {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async createScaffold(preset?: Partial<UaAutomationDetailModel>) {
        const scaffold: UaAutomationDetailModel = {
            unique: UA_EMPTY_GUID,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            alias: "",
            name: "",
            description: null,
            workspaceId: UA_EMPTY_GUID,
            groupId: null,
            status: "Draft",
            publishedVersion: null,
            draftVersion: 0,
            trigger: null,
            steps: [],
            connections: [],
            canvasState: null,
            notificationSettings: null,
            version: 0,
            dateCreated: new Date().toISOString(),
            dateModified: new Date().toISOString(),
            health: "Healthy",
            warningIssuedUtc: null,
            disabledUtc: null,
            ...preset,
        };

        return { data: scaffold };
    }

    async read(unique: string) {
        const { data, error } = await tryExecute(
            this.#host,
            AutomationsService.getAutomationsById({ path: { id: unique } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaAutomationTypeMapper.toDetailModel(data) };
    }

    async create(model: UaAutomationDetailModel, _parentUnique: string | null) {
        const requestBody = UaAutomationTypeMapper.toCreateRequest(model);

        // throwOnError: tryExecute only notifies (toast) on a rejected promise, and the generated SDK
        // client resolves 4xx/5xx by default, so a failed save would otherwise report nothing.
        const { response, error } = await tryExecute(
            this.#host,
            AutomationsService.postAutomations({ body: requestBody, throwOnError: true }),
        );

        if (error) {
            return { error };
        }

        const locationHeader = response?.headers?.get("Location") ?? "";
        const unique = locationHeader.split("/").pop() ?? "";

        return this.read(unique);
    }

    async update(model: UaAutomationDetailModel) {
        const requestBody = UaAutomationTypeMapper.toUpdateRequest(model);

        // throwOnError: see create(). A 409 here means another tab or user saved first.
        const { error } = await tryExecute(
            this.#host,
            AutomationsService.putAutomationsById({
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
            AutomationsService.deleteAutomationsById({ path: { id: unique } }),
        );

        if (error) {
            return { error };
        }

        return {};
    }

    async readGroup(groupId: string) {
        const { data, error } = await tryExecute(
            this.#host,
            AutomationsService.getAutomationsGroupsByGroupId({ path: { groupId } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data };
    }
}
