import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { RunsService } from "../../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../../type-mapper.js";

export class UaRunDetailServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async read(unique: string) {
        const { data, error } = await tryExecute(
            this.#host,
            RunsService.getRunsById({ path: { id: unique } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaRunTypeMapper.toDetailModel(data) };
    }
}
