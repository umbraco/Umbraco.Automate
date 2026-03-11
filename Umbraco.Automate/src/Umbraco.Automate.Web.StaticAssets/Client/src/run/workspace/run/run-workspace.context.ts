import type { UmbRoutableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import { UmbWorkspaceRouteManager, UmbSubmittableWorkspaceContextBase } from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbBasicState, UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import { UmbEntityContext } from "@umbraco-cms/backoffice/entity";
import { UaRunDetailServerDataSource } from "../../repository/detail/run-detail.server.data-source.js";
import { UaAutomationDetailServerDataSource } from "../../../automation/repository/detail/automation-detail.server.data-source.js";
import { UA_RUN_WORKSPACE_ALIAS, UA_RUN_ENTITY_TYPE } from "../../constants.js";
import type { UaRunDetailModel } from "../../types.js";
import type { UaAutomationDetailModel } from "../../../automation/types.js";
import { UaRunWorkspaceEditorElement } from "./run-workspace-editor.element.js";

export class UaRunWorkspaceContext
    extends UmbSubmittableWorkspaceContextBase<UaRunDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);

    #unique = new UmbBasicState<string | undefined>(undefined);
    readonly unique = this.#unique.asObservable();

    #run = new UmbObjectState<UaRunDetailModel | undefined>(undefined);
    readonly run = this.#run.asObservable();

    #automation = new UmbObjectState<UaAutomationDetailModel | undefined>(undefined);
    readonly automation = this.#automation.asObservable();

    #runDataSource: UaRunDetailServerDataSource;
    #automationDataSource: UaAutomationDetailServerDataSource;
    #entityContext = new UmbEntityContext(this);

    constructor(host: UmbControllerHost) {
        super(host, UA_RUN_WORKSPACE_ALIAS);

        this.#runDataSource = new UaRunDetailServerDataSource(this);
        this.#automationDataSource = new UaAutomationDetailServerDataSource(this);

        this.#entityContext.setEntityType(UA_RUN_ENTITY_TYPE);
        this.observe(this.unique, (unique) => this.#entityContext.setUnique(unique ?? null));
        this.observe(this.run, (run) => {
            if (run) {
                this.view.setTitle(`Run ${run.status}`);
            }
        });

        this.routes.setRoutes([
            {
                path: "edit/:unique",
                component: UaRunWorkspaceEditorElement,
                setup: (_component, info) => {
                    this.load(info.match.params.unique);
                },
            },
        ]);
    }

    protected resetState(): void {
        super.resetState();
        this.#unique.setValue(undefined);
        this.#run.setValue(undefined);
        this.#automation.setValue(undefined);
    }

    async load(id: string) {
        this.resetState();

        const { data: run } = await this.#runDataSource.read(id);
        if (!run) return;

        this.#unique.setValue(run.unique);
        this.#run.setValue(run);
        this.setIsNew(false);

        // Load the associated automation for canvas rendering
        const { data: automation } = await this.#automationDataSource.read(run.automationId);
        if (automation) {
            this.#automation.setValue(automation);
        }
    }

    getData(): UaRunDetailModel | undefined {
        return this.#run.getValue();
    }

    getUnique(): string | undefined {
        return this.#unique.getValue();
    }

    getEntityType(): string {
        return UA_RUN_ENTITY_TYPE;
    }

    async submit() {
        // Runs are read-only, no submit needed
    }
}

export { UaRunWorkspaceContext as api };
