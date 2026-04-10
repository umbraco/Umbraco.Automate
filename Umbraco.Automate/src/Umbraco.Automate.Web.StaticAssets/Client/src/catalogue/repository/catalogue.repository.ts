import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { CatalogueService } from "../../api/sdk.gen.js";
import { UaCatalogueServerDataSource } from "./catalogue.server.data-source.js";
import type { NotificationChannelItemResponseModel } from "../../api/types.gen.js";
import type { UaActionCatalogueItemModel, UaConnectionTypeCatalogueItemModel, UaControlFlowCatalogueItemModel, UaTriggerCatalogueItemModel } from "../types.js";

export class UaCatalogueRepository extends UmbRepositoryBase {
    #dataSource: UaCatalogueServerDataSource;
    #actionsCache: UaActionCatalogueItemModel[] | undefined;
    #triggersCache: UaTriggerCatalogueItemModel[] | undefined;
    #connectionTypesCache: UaConnectionTypeCatalogueItemModel[] | undefined;
    #controlFlowsCache: UaControlFlowCatalogueItemModel[] | undefined;

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

    async requestConnectionTypes(): Promise<{ data?: UaConnectionTypeCatalogueItemModel[]; error?: unknown }> {
        if (this.#connectionTypesCache) {
            return { data: this.#connectionTypesCache };
        }

        const result = await this.#dataSource.getConnectionTypes();
        if (result.data) {
            this.#connectionTypesCache = result.data;
        }
        return result;
    }

    async requestControlFlows(): Promise<{ data?: UaControlFlowCatalogueItemModel[]; error?: unknown }> {
        if (this.#controlFlowsCache) {
            return { data: this.#controlFlowsCache };
        }

        const result = await this.#dataSource.getControlFlows();
        if (result.data) {
            this.#controlFlowsCache = result.data;
        }
        return result;
    }

    async requestNotificationChannels(): Promise<{ data?: NotificationChannelItemResponseModel[]; error?: unknown }> {
        const { data, error } = await tryExecute(
            this,
            CatalogueService.getCatalogueNotificationChannels(),
        );
        return { data: data ?? undefined, error };
    }

    clearCache() {
        this.#actionsCache = undefined;
        this.#triggersCache = undefined;
        this.#connectionTypesCache = undefined;
        this.#controlFlowsCache = undefined;
    }
}

export { UaCatalogueRepository as api };
