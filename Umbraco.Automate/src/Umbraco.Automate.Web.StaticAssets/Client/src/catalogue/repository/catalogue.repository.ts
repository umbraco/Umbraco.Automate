import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UaCatalogueServerDataSource } from "./catalogue.server.data-source.js";
import type { UaActionCatalogueItemModel, UaTriggerCatalogueItemModel } from "../types.js";

export class UaCatalogueRepository extends UmbRepositoryBase {
    #dataSource: UaCatalogueServerDataSource;
    #actionsCache: UaActionCatalogueItemModel[] | undefined;
    #triggersCache: UaTriggerCatalogueItemModel[] | undefined;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#dataSource = new UaCatalogueServerDataSource(host);
    }

    async requestActions(): Promise<{ data?: UaActionCatalogueItemModel[]; error?: unknown }> {
        if (this.#actionsCache) {
            return { data: this.#actionsCache };
        }

        const result = await this.#dataSource.getActions();
        if (result.data) {
            this.#actionsCache = result.data;
        }
        return result;
    }

    async requestTriggers(): Promise<{ data?: UaTriggerCatalogueItemModel[]; error?: unknown }> {
        if (this.#triggersCache) {
            return { data: this.#triggersCache };
        }

        const result = await this.#dataSource.getTriggers();
        if (result.data) {
            this.#triggersCache = result.data;
        }
        return result;
    }

    clearCache() {
        this.#actionsCache = undefined;
        this.#triggersCache = undefined;
    }
}

export { UaCatalogueRepository as api };
