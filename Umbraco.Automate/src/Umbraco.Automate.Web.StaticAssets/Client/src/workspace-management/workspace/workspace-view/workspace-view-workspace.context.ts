import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import {
    UmbWorkspaceRouteManager,
    type UmbRoutableWorkspaceContext,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import { UmbEntityContext } from "@umbraco-cms/backoffice/entity";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UA_WORKSPACE_VIEW_WORKSPACE_ALIAS } from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";
import { UmbWorkspaceEditorElement } from "@umbraco-cms/backoffice/workspace";

export class UaWorkspaceViewWorkspaceContext extends UmbContextBase implements UmbRoutableWorkspaceContext {
    public readonly workspaceAlias = UA_WORKSPACE_VIEW_WORKSPACE_ALIAS;
    public readonly routes = new UmbWorkspaceRouteManager(this);
    public readonly view = new UmbViewContext(this, null);

    #entityContext = new UmbEntityContext(this);

    constructor(host: UmbControllerHost) {
        super(host, UMB_WORKSPACE_CONTEXT.toString());
        this.#entityContext.setEntityType(UA_WORKSPACE_ENTITY_TYPE);

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

export { UaWorkspaceViewWorkspaceContext as api };
