import type { UmbRoutableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import {
    UmbEntityDetailWorkspaceContextBase,
    UmbWorkspaceRouteManager,
    UmbWorkspaceIsNewRedirectController,
    UmbWorkspaceIsNewRedirectControllerAlias,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
    UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
    UA_WORKSPACE_MGMT_ENTITY_TYPE,
    UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE,
} from "../../constants.js";
import { UA_WORKSPACE_DETAIL_REPOSITORY_ALIAS } from "../../repository/constants.js";
import type { UaWorkspaceDetailModel } from "../../types.js";
import { UaWorkspaceMgmtWorkspaceEditorElement } from "./workspace-mgmt-workspace-editor.element.js";

export class UaWorkspaceMgmtWorkspaceContext
    extends UmbEntityDetailWorkspaceContextBase<UaWorkspaceDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);

    constructor(host: UmbControllerHost) {
        super(host, {
            workspaceAlias: UA_WORKSPACE_MGMT_WORKSPACE_ALIAS,
            entityType: UA_WORKSPACE_MGMT_ENTITY_TYPE,
            detailRepositoryAlias: UA_WORKSPACE_DETAIL_REPOSITORY_ALIAS,
        });

        this.observe(this.data, (data) => this.view.setTitle(data?.name), null);

        this.routes.setRoutes([
            {
                path: "create",
                component: UaWorkspaceMgmtWorkspaceEditorElement,
                setup: async () => {
                    await this.createScaffold({
                        parent: { unique: null, entityType: UA_WORKSPACE_MGMT_ROOT_ENTITY_TYPE },
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
                component: UaWorkspaceMgmtWorkspaceEditorElement,
                setup: (_component, info) => {
                    this.removeUmbControllerByAlias(UmbWorkspaceIsNewRedirectControllerAlias);
                    this.load(info.match.params.unique);
                },
            },
        ]);
    }

    updateProperty<K extends keyof UaWorkspaceDetailModel>(key: K, value: UaWorkspaceDetailModel[K]) {
        this._data.updateCurrent({ [key]: value } as Partial<UaWorkspaceDetailModel>);
    }
}

export { UaWorkspaceMgmtWorkspaceContext as api };
