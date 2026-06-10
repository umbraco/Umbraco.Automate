import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { CatalogueService } from "../../api/sdk.gen.js";
import { UaCatalogueTypeMapper } from "../type-mapper.js";
import type { UaActionCatalogueItemModel, UaConnectionTypeCatalogueItemModel, UaControlFlowCatalogueItemModel, UaTriggerCatalogueItemModel } from "../types.js";

export class UaCatalogueServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async getActions(workspaceId?: string): Promise<{ data?: UaActionCatalogueItemModel[]; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            CatalogueService.getCatalogueActions({ query: workspaceId ? { workspaceId } : undefined }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: data.map(UaCatalogueTypeMapper.toActionModel) };
    }

    async getTriggers(workspaceId?: string): Promise<{ data?: UaTriggerCatalogueItemModel[]; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            CatalogueService.getCatalogueTriggers({ query: workspaceId ? { workspaceId } : undefined }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: data.map(UaCatalogueTypeMapper.toTriggerModel) };
    }

    async getConnectionTypes(): Promise<{ data?: UaConnectionTypeCatalogueItemModel[]; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            CatalogueService.getCatalogueConnectionTypes(),
        );

        if (error || !data) {
            return { error };
        }

        return { data: data.map(UaCatalogueTypeMapper.toConnectionTypeModel) };
    }

    async getControlFlows(): Promise<{ data?: UaControlFlowCatalogueItemModel[]; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            CatalogueService.getCatalogueControlFlows(),
        );

        if (error || !data) {
            return { error };
        }

        return { data: data.map(UaCatalogueTypeMapper.toControlFlowModel) };
    }

    /**
     * Resolves the output schema for a step type using its configured settings. Used for step types
     * that declare `hasDynamicOutputSchema` (e.g. Run AI Agent), whose output shape depends on settings.
     * Returns a null schema when the settings are insufficient to determine the shape (e.g. no agent selected).
     */
    async resolveOutputSchema(
        alias: string,
        settings: Record<string, unknown>,
    ): Promise<{ data?: Record<string, unknown> | null; error?: unknown }> {
        const { data, error } = await tryExecute(
            this.#host,
            CatalogueService.postCatalogueStepTypesByAliasOutputSchema({
                path: { alias },
                body: { settings },
            }),
        );

        if (error) {
            return { error };
        }

        return { data: (data as Record<string, unknown> | null) ?? null };
    }
}
