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
import { UmbHintController } from "@umbraco-cms/backoffice/hint";
import {
    UA_AUTOMATION_WORKSPACE_ALIAS,
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_GROUP_ENTITY_TYPE,
} from "../../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../../workspace-management/constants.js";
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
    readonly name = this._data.createObservablePartOfCurrent((data) => data?.name);
    readonly description = this._data.createObservablePartOfCurrent((data) => data?.description);

    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

    // Hint controller for badging workspace view tabs (e.g. the Info tab when the breaker
    // has tripped). Provided at this context so the workspace-editor's view contexts inherit it.
    #hintController = new UmbHintController(this);
    static readonly #HEALTH_HINT_UNIQUE = "ua-automation-health-hint";
    static readonly #INFO_VIEW_ALIAS = "UmbracoAutomate.Workspace.Automation.View.Info";

    constructor(host: UmbControllerHost) {
        super(host, {
            workspaceAlias: UA_AUTOMATION_WORKSPACE_ALIAS,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            detailRepositoryAlias: UA_AUTOMATION_DETAIL_REPOSITORY_ALIAS,
        });

        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            this.#eventContext = context;
        });

        this.observe(this.name, (name) => this.view.setTitle(name), null);

        // Badge the Info tab whenever circuit-breaker health is unhealthy.
        this.#hintController.provideAt(this);
        this.observe(
            this._data.createObservablePartOfCurrent((d) => d?.health),
            (health) => this.#updateHealthHint(health),
            null,
        );

        this.routes.setRoutes([
            {
                path: "create/:parentEntityType/:parentUnique",
                component: UaAutomationWorkspaceEditorElement,
                setup: async (_component, info) => {
                    const parentEntityType = decodeURIComponent(info.match.params.parentEntityType);
                    const rawParentUnique = decodeURIComponent(info.match.params.parentUnique);
                    const parentUnique = rawParentUnique === "null" ? null : rawParentUnique;

                    const preset: Partial<UaAutomationDetailModel> = {};

                    if (parentEntityType === UA_WORKSPACE_ENTITY_TYPE && parentUnique) {
                        preset.workspaceId = parentUnique;
                    } else if (parentEntityType === UA_AUTOMATION_GROUP_ENTITY_TYPE && parentUnique) {
                        preset.groupId = parentUnique;
                        const workspaceId = await this.#resolveWorkspaceForGroup(parentUnique);
                        if (workspaceId) {
                            preset.workspaceId = workspaceId;
                        }
                    }

                    await this.createScaffold({
                        parent: {
                            unique: parentUnique ?? null,
                            entityType: parentEntityType,
                        },
                        preset,
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

    updateProperties(properties: Partial<UaAutomationDetailModel>) {
        this._data.updateCurrent(properties);
    }

    async publish() {
        const unique = this.getUnique();
        if (!unique || unique === UA_EMPTY_GUID) return;

        const { error } = await tryExecute(
            this,
            AutomationsService.postAutomationsByIdPublish({ path: { id: unique } }),
        );

        if (error) throw error;

        await this.reload();
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

        await this.reload();
        this.#reloadStructure(unique);
    }

    async #resolveWorkspaceForGroup(groupId: string): Promise<string | undefined> {
        const { data } = await tryExecute(
            this,
            AutomationsService.getAutomationsGroupsByGroupId({ path: { groupId } }),
        );
        return data?.workspaceId;
    }

    #reloadStructure(unique: string) {
        const event = new UmbRequestReloadStructureForEntityEvent({
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            unique,
        });
        this.#eventContext?.dispatchEvent(event);
    }

    #updateHealthHint(health: string | undefined) {
        const unique = UaAutomationWorkspaceContext.#HEALTH_HINT_UNIQUE;
        if (this.#hintController.has(unique)) {
            this.#hintController.removeOne(unique);
        }

        if (health === "Disabled") {
            this.#hintController.addOne({
                unique,
                path: [UaAutomationWorkspaceContext.#INFO_VIEW_ALIAS],
                text: "!",
                color: "danger",
            });
        } else if (health === "Degraded") {
            this.#hintController.addOne({
                unique,
                path: [UaAutomationWorkspaceContext.#INFO_VIEW_ALIAS],
                text: "!",
                color: "warning",
            });
        }
    }
}

export { UaAutomationWorkspaceContext as api };
