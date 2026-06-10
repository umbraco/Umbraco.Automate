import type { UmbRoutableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import {
    UmbEntityDetailWorkspaceContextBase,
    UmbWorkspaceRouteManager,
    UmbWorkspaceIsNewRedirectController,
    UmbWorkspaceIsNewRedirectControllerAlias,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
    UA_CONNECTION_WORKSPACE_ALIAS,
    UA_CONNECTION_ENTITY_TYPE,
    UA_CONNECTION_ROOT_ENTITY_TYPE,
} from "../../constants.js";
import { UA_CONNECTION_DETAIL_REPOSITORY_ALIAS } from "../../repository/constants.js";
import type { UaConnectionDetailModel } from "../../types.js";
import { UaConnectionWorkspaceEditorElement } from "./connection-workspace-editor.element.js";

export class UaConnectionWorkspaceContext
    extends UmbEntityDetailWorkspaceContextBase<UaConnectionDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);
    readonly name = this._data.createObservablePartOfCurrent((data) => data?.name);

    constructor(host: UmbControllerHost) {
        super(host, {
            workspaceAlias: UA_CONNECTION_WORKSPACE_ALIAS,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            detailRepositoryAlias: UA_CONNECTION_DETAIL_REPOSITORY_ALIAS,
        });

        this.observe(this.name, (name) => this.view.setTitle(name), null);

        this.routes.setRoutes([
            {
                path: "create/:connectionType",
                component: UaConnectionWorkspaceEditorElement,
                setup: async (_component, info) => {
                    const connectionType = decodeURIComponent(info.match.params.connectionType);
                    await this.createScaffold({
                        parent: { unique: null, entityType: UA_CONNECTION_ROOT_ENTITY_TYPE },
                        preset: { type: connectionType },
                    });
                    new UmbWorkspaceIsNewRedirectController(
                        this,
                        this,
                        this.getHostElement().shadowRoot!.querySelector("umb-router-slot")!,
                    );
                },
            },
            {
                path: "edit/:unique",
                component: UaConnectionWorkspaceEditorElement,
                setup: (_component, info) => {
                    this.removeUmbControllerByAlias(UmbWorkspaceIsNewRedirectControllerAlias);
                    this.load(info.match.params.unique);
                },
            },
        ]);
    }

    updateProperty<K extends keyof UaConnectionDetailModel>(key: K, value: UaConnectionDetailModel[K]) {
        this._data.updateCurrent({ [key]: value } as Partial<UaConnectionDetailModel>);
    }
}

export { UaConnectionWorkspaceContext as api };
