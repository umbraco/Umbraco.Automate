import type { UauiCompare, UauiDateRange } from "@umbraco/analytics-ui";
import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbEntityContext } from "@umbraco-cms/backoffice/entity";
import { UmbArrayState, UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UMB_WORKSPACE_CONTEXT, type UmbWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { coreClientReady } from "../../core/client-ready.js";
import { UaWorkspaceCollectionServerDataSource } from "../../workspace-management/repository/collection/workspace-collection.server.data-source.js";
import type { UaWorkspaceItemModel } from "../../workspace-management/types.js";
import { UA_ANALYTICS_STORAGE_KEY } from "../constants.js";
import {
    createDefaultFilters,
    readStoredFilters,
    writeStoredFilters,
    type UaAnalyticsFilters,
} from "./analytics-filters.js";

const WORKSPACE_PAGE_SIZE = 100;

/**
 * The Analytics workspace. It is the workspace context the backoffice looks for
 * (`UMB_WORKSPACE_CONTEXT`) and it holds what every Analytics view is filtered by: the date range,
 * the comparison, the workspace and the browser's time zone. The filters are remembered per user in
 * `localStorage`.
 */
export class UaAnalyticsWorkspaceContext extends UmbContextBase implements UmbWorkspaceContext {
    workspaceAlias = "";

    #entityContext = new UmbEntityContext(this);
    readonly view = new UmbViewContext(this, null);

    /** Undefined until the remembered filters and the user's workspaces are known, so views load once. */
    #filters = new UmbObjectState<UaAnalyticsFilters | undefined>(undefined);
    readonly filters = this.#filters.asObservable();

    #workspaces = new UmbArrayState<UaWorkspaceItemModel>([], (workspace) => workspace.unique);
    /** The workspaces the user can access. */
    readonly workspaces = this.#workspaces.asObservable();

    #storageKey?: string;
    #destroyed = false;

    constructor(host: UmbControllerHost) {
        super(host, UMB_WORKSPACE_CONTEXT.toString());
        this.#entityContext.setUnique(null);
        this.#initialize().catch((error) => console.error("Analytics workspace failed to initialize", error));
    }

    set manifest(manifest: { alias: string; meta: { entityType: string; headline?: string } }) {
        this.workspaceAlias = manifest.alias;
        this.#entityContext.setEntityType(manifest.meta.entityType);
        this.view.setTitle(manifest.meta.headline);
    }

    getEntityType(): string {
        return this.#entityContext.getEntityType()!;
    }

    getUnique() {
        return this.#entityContext.getUnique() ?? null;
    }

    getFilters(): UaAnalyticsFilters | undefined {
        return this.#filters.getValue();
    }

    /** Sets the range and its comparison together, as `uaui-date-range` raises them. */
    setDateRange(range: UauiDateRange, compare?: UauiCompare) {
        this.#update({ range, compare });
    }

    setWorkspace(workspaceId?: string) {
        this.#update({ workspaceId: workspaceId || undefined });
    }

    #update(changes: Partial<UaAnalyticsFilters>) {
        const current = this.#filters.getValue();
        if (!current) return;

        const next = { ...current, ...changes };
        this.#filters.setValue(next);
        if (this.#storageKey) writeStoredFilters(this.#storageKey, next);
    }

    async #initialize() {
        const [userKey, workspaces] = await Promise.all([this.#getUserKey(), this.#loadWorkspaces()]);
        // The editor can leave Analytics before these resolve; the states are gone by then.
        if (this.#destroyed) return;
        this.#workspaces.setValue(workspaces);
        this.#storageKey = userKey ? `${UA_ANALYTICS_STORAGE_KEY}:${userKey}` : undefined;

        const stored = this.#storageKey ? readStoredFilters(this.#storageKey) : undefined;
        const filters = stored ?? createDefaultFilters();

        // A remembered workspace the user can no longer access (or that was deleted) means all workspaces.
        if (filters.workspaceId && !workspaces.some((workspace) => workspace.unique === filters.workspaceId)) {
            filters.workspaceId = undefined;
        }

        this.#filters.setValue(filters);
    }

    async #getUserKey(): Promise<string | undefined> {
        const currentUser = await this.getContext(UMB_CURRENT_USER_CONTEXT);
        if (!currentUser) return undefined;

        await currentUser.load();
        return currentUser.getUnique();
    }

    async #loadWorkspaces(): Promise<Array<UaWorkspaceItemModel>> {
        // The generated client has no base URL or auth until app.ts has configured it.
        await coreClientReady;

        const source = new UaWorkspaceCollectionServerDataSource(this);
        const workspaces: Array<UaWorkspaceItemModel> = [];
        for (let skip = 0; ; skip += WORKSPACE_PAGE_SIZE) {
            const { data } = await source.getCollection({ skip, take: WORKSPACE_PAGE_SIZE });
            if (!data || this.#destroyed) break;

            workspaces.push(...data.items);
            if (skip + WORKSPACE_PAGE_SIZE >= data.total) break;
        }

        return workspaces.sort((a, b) => a.name.localeCompare(b.name));
    }

    override destroy() {
        this.#destroyed = true;
        this.#filters.destroy();
        this.#workspaces.destroy();
        super.destroy();
    }
}

export { UaAnalyticsWorkspaceContext as api };
