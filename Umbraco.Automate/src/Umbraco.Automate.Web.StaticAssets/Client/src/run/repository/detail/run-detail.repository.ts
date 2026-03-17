import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UaRunDetailServerDataSource } from "./run-detail.server.data-source.js";
import type { UaRunDetailModel } from "../../types.js";

export class UaRunDetailRepository extends UmbRepositoryBase {
    #dataSource: UaRunDetailServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#dataSource = new UaRunDetailServerDataSource(host);
    }

    async requestByUnique(unique: string): Promise<{ data?: UaRunDetailModel; error?: unknown }> {
        return this.#dataSource.read(unique);
    }
}

export { UaRunDetailRepository as api };
