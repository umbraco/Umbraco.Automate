import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { CatalogueService } from "../../api/sdk.gen.js";
import { UaCatalogueServerDataSource } from "./catalogue.server.data-source.js";
import type { NotificationChannelItemResponseModel } from "../../api/types.gen.js";
import type { UaActionCatalogueItemModel, UaConnectionTypeCatalogueItemModel, UaControlFlowCatalogueItemModel, UaTriggerCatalogueItemModel } from "../types.js";

const UNSCOPED_CACHE_KEY = "__all__";

export class UaCatalogueRepository extends UmbRepositoryBase {
    #dataSource: UaCatalogueServerDataSource;
    // Cached per workspace scope — different workspaces filter to different subsets, so a flat
    // cache would return the wrong set when the picker is opened against another workspace.
    #actionsCache = new Map<string, UaActionCatalogueItemModel[]>();
    #triggersCache = new Map<string, UaTriggerCatalogueItemModel[]>();
    #connectionTypesCache: UaConnectionTypeCatalogueItemModel[] | undefined;
    #controlFlowsCache: UaControlFlowCatalogueItemModel[] | undefined;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#dataSource = new UaCatalogueServerDataSource(host);
    }

    async requestActions(workspaceId?: string): Promise<{ data?: UaActionCatalogueItemModel[]; error?: unknown }> {
        const key = workspaceId ?? UNSCOPED_CACHE_KEY;
        const cached = this.#actionsCache.get(key);
        if (cached) {
            return { data: cached };
        }

        const result = await this.#dataSource.getActions(workspaceId);
        if (result.data) {
            this.#actionsCache.set(key, result.data);
        }
        return result;
    }

    async requestTriggers(workspaceId?: string): Promise<{ data?: UaTriggerCatalogueItemModel[]; error?: unknown }> {
        const key = workspaceId ?? UNSCOPED_CACHE_KEY;
        const cached = this.#triggersCache.get(key);
        if (cached) {
            return { data: cached };
        }

        const result = await this.#dataSource.getTriggers(workspaceId);
        if (result.data) {
            this.#triggersCache.set(key, result.data);
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
        this.#actionsCache.clear();
        this.#triggersCache.clear();
        this.#connectionTypesCache = undefined;
        this.#controlFlowsCache = undefined;
    }
}

export { UaCatalogueRepository as api };
