import type { UmbRoutableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import {
    UmbEntityDetailWorkspaceContextBase,
    UmbWorkspaceRouteManager,
    UmbWorkspaceIsNewRedirectController,
    UmbWorkspaceIsNewRedirectControllerAlias,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadStructureForEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import {
    UA_AUTOMATION_WORKSPACE_ALIAS,
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_WORKSPACE_ENTITY_TYPE,
} from "../../constants.js";
import { UA_AUTOMATION_DETAIL_REPOSITORY_ALIAS } from "../../repository/constants.js";
import type { UaAutomationDetailModel } from "../../types.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";
import { UaAutomationWorkspaceEditorElement } from "./automation-workspace-editor.element.js";
import { AutomationsService } from "../../../api/sdk.gen.js";

export class UaAutomationWorkspaceContext
    extends UmbEntityDetailWorkspaceContextBase<UaAutomationDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);

    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

    constructor(host: UmbControllerHost) {
        super(host, {
            workspaceAlias: UA_AUTOMATION_WORKSPACE_ALIAS,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            detailRepositoryAlias: UA_AUTOMATION_DETAIL_REPOSITORY_ALIAS,
        });

        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            this.#eventContext = context;
        });

        this.observe(this.data, (data) => this.view.setTitle(data?.name), null);

        this.routes.setRoutes([
            {
                path: "create/:parentEntityType/:parentUnique",
                component: UaAutomationWorkspaceEditorElement,
                setup: async (_component, info) => {
                    const parentEntityType = info.match.params.parentEntityType;
                    const parentUnique = info.match.params.parentUnique;
                    const isWorkspaceParent =
                        parentEntityType === UA_AUTOMATION_WORKSPACE_ENTITY_TYPE &&
                        parentUnique &&
                        parentUnique !== "null";

                    await this.createScaffold({
                        parent: {
                            unique: parentUnique === "null" ? null : parentUnique,
                            entityType: parentEntityType,
                        },
                        preset: isWorkspaceParent
                            ? ({ workspaceId: parentUnique } as Partial<UaAutomationDetailModel>)
                            : undefined,
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
                component: UaAutomationWorkspaceEditorElement,
                setup: (_component, info) => {
                    this.removeUmbControllerByAlias(UmbWorkspaceIsNewRedirectControllerAlias);
                    this.load(info.match.params.unique);
                },
            },
        ]);
    }

    updateProperty<K extends keyof UaAutomationDetailModel>(key: K, value: UaAutomationDetailModel[K]) {
        this._data.updateCurrent({ [key]: value } as Partial<UaAutomationDetailModel>);
    }

    async publish() {
        const unique = this.getUnique();
        if (!unique || unique === UA_EMPTY_GUID) return;

        const { error } = await tryExecute(
            this,
            AutomationsService.postAutomationsByIdPublish({ path: { id: unique } }),
        );

        if (error) throw error;

        await this.load(unique);
        this.#reloadStructure(unique);
    }

    async unpublish() {
        const unique = this.getUnique();
        if (!unique || unique === UA_EMPTY_GUID) return;

        const { error } = await tryExecute(
            this,
            AutomationsService.postAutomationsByIdUnpublish({ path: { id: unique } }),
        );

        if (error) throw error;

        await this.load(unique);
        this.#reloadStructure(unique);
    }

    #reloadStructure(unique: string) {
        const event = new UmbRequestReloadStructureForEntityEvent({
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            unique,
        });
        this.#eventContext?.dispatchEvent(event);
    }
}

export { UaAutomationWorkspaceContext as api };
