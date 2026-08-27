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
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { UmbRequestReloadStructureForEntityEvent } from "@umbraco-cms/backoffice/entity-action";
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
import { computeReachableFromTrigger } from "./canvas/utils/model-to-flow.js";

export class UaAutomationWorkspaceContext
    extends UmbEntityDetailWorkspaceContextBase<UaAutomationDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);
    readonly name = this._data.createObservablePartOfCurrent((data) => data?.name);
    readonly description = this._data.createObservablePartOfCurrent((data) => data?.description);

    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;
    #localize = new UmbLocalizationController(this);

    // Workspace view tabs (UmbWorkspaceViewContext) inherit hints from the parent UmbViewContext
    // — i.e. this workspace's `this.view`. Adding a hint to that controller with the child view's
    // alias as path[0] makes the workspace-editor badge that tab.
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

    /**
     * Save & Publish calls submit() before publish(), so hooking submit() here covers both the
     * plain Save action (CMS-default UmbSubmitWorkspaceAction) and Save & Publish with one check.
     */
    override async submit() {
        await this.#warnIfDisconnectedSteps();
        return super.submit();
    }

    /**
     * Non-blocking heads-up for steps with no path back to the trigger. WorkflowCompiler.
     * TopologicalSort silently drops these from the compiled workflow (see model-to-flow.ts's
     * computeReachableFromTrigger, which mirrors that same reachability pass), so without this a
     * step can sit on the canvas looking fully configured while never actually running. Save and
     * publish still proceed — this only surfaces the toast.
     */
    async #warnIfDisconnectedSteps() {
        const data = this.getData();
        if (!data?.trigger) return;

        const reachable = computeReachableFromTrigger(data.connections);
        const disconnectedNames = data.steps.filter((s) => !reachable.has(s.id)).map((s) => s.name);
        if (disconnectedNames.length === 0) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
        notifications?.peek("warning", {
            data: {
                headline: this.#localize.term("uaAutomation_disconnectedStepsWarningHeadline"),
                message:
                    disconnectedNames.length === 1
                        ? this.#localize.term("uaAutomation_disconnectedStepsWarningOne", disconnectedNames[0])
                        : this.#localize.term(
                              "uaAutomation_disconnectedStepsWarningMany",
                              disconnectedNames.length,
                              disconnectedNames.join(", "),
                          ),
            },
        });
    }

    async publish() {
        const unique = this.getUnique();
        if (!unique || unique === UA_EMPTY_GUID) return;

        const { error } = await tryExecute(
            this,
            // throwOnError: tryExecute only auto-notifies (toast) and populates `error` on a
            // rejected promise — the generated SDK client resolves 4xx/5xx responses normally
            // by default, which would otherwise leave publish failures (e.g. dangling binding
            // references) silently unreported in the UI.
            AutomationsService.postAutomationsByIdPublish({ path: { id: unique }, throwOnError: true }),
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
            AutomationsService.postAutomationsByIdUnpublish({ path: { id: unique }, throwOnError: true }),
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
        const hints = this.view.hints;

        if (hints.has(unique)) {
            hints.removeOne(unique);
        }

        if (health === "Disabled") {
            hints.addOne({
                unique,
                path: [UaAutomationWorkspaceContext.#INFO_VIEW_ALIAS],
                text: "!",
                color: "danger",
            });
        } else if (health === "Degraded") {
            hints.addOne({
                unique,
                path: [UaAutomationWorkspaceContext.#INFO_VIEW_ALIAS],
                text: "!",
                color: "warning",
            });
        }
    }
}

export { UaAutomationWorkspaceContext as api };
