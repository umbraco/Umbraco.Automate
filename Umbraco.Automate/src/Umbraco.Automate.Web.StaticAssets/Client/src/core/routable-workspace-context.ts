import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import {
    UmbWorkspaceRouteManager,
    type UmbRoutableWorkspaceContext,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import { UmbEntityContext } from "@umbraco-cms/backoffice/entity";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UmbWorkspaceEditorElement } from "@umbraco-cms/backoffice/workspace";

/**
 * A minimal routable workspace context that sets up edit/:unique routing
 * and exposes entity type + unique via UmbEntityContext.
 *
 * Use this for workspaces that only need to resolve their entity identity
 * (e.g. folder/group workspaces, read-only workspace views).
 */
export class UaRoutableWorkspaceContext extends UmbContextBase implements UmbRoutableWorkspaceContext {
    public readonly workspaceAlias: string;
    public readonly routes = new UmbWorkspaceRouteManager(this);
    public readonly view = new UmbViewContext(this, null);

    #entityContext = new UmbEntityContext(this);

    constructor(host: UmbControllerHost, workspaceAlias: string, entityType: string) {
        super(host, UMB_WORKSPACE_CONTEXT.toString());
        this.workspaceAlias = workspaceAlias;
        this.#entityContext.setEntityType(entityType);

        this.routes.setRoutes([
            {
                path: "edit/:unique",
                component: UmbWorkspaceEditorElement,
                setup: (_component, info) => {
                    const unique = info.match.params.unique;
                    this.#entityContext.setUnique(unique);
                },
            },
        ]);
    }

    getEntityType() {
        return this.#entityContext.getEntityType()!;
    }

    getUnique() {
        return this.#entityContext.getUnique();
    }
}
