import type { UmbRoutableWorkspaceContext } from "@umbraco-cms/backoffice/workspace";
import {
    UmbWorkspaceRouteManager,
    UmbSubmittableWorkspaceContextBase,
    UmbWorkspaceIsNewRedirectController,
    UmbWorkspaceIsNewRedirectControllerAlias,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbBasicState, UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import { UmbEntityContext } from "@umbraco-cms/backoffice/entity";
import { UmbValidationContext } from "@umbraco-cms/backoffice/validation";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import {
    UmbRequestReloadStructureForEntityEvent,
    UmbRequestReloadChildrenOfEntityEvent,
} from "@umbraco-cms/backoffice/entity-action";
import { UaAutomationDetailRepository } from "../../repository/detail/automation-detail.repository.js";
import {
    UA_AUTOMATION_WORKSPACE_ALIAS,
    UA_AUTOMATION_ENTITY_TYPE,
    UA_AUTOMATION_ROOT_ENTITY_TYPE,
} from "../../constants.js";
import type { UaAutomationDetailModel } from "../../types.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";
import { UaAutomationWorkspaceEditorElement } from "./automation-workspace-editor.element.js";
import { AutomationsService } from "../../../api/sdk.gen.js";

export class UaAutomationWorkspaceContext
    extends UmbSubmittableWorkspaceContextBase<UaAutomationDetailModel>
    implements UmbRoutableWorkspaceContext
{
    readonly routes = new UmbWorkspaceRouteManager(this);

    #unique = new UmbBasicState<string | undefined>(undefined);
    readonly unique = this.#unique.asObservable();

    #model = new UmbObjectState<UaAutomationDetailModel | undefined>(undefined);
    readonly model = this.#model.asObservable();

    #repository: UaAutomationDetailRepository;
    #entityContext = new UmbEntityContext(this);
    #validationContext = new UmbValidationContext(this);
    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

    get validation() {
        return this.#validationContext;
    }

    constructor(host: UmbControllerHost) {
        super(host, UA_AUTOMATION_WORKSPACE_ALIAS);

        this.#repository = new UaAutomationDetailRepository(this);
        this.addValidationContext(this.#validationContext);

        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            this.#eventContext = context;
        });

        this.#entityContext.setEntityType(UA_AUTOMATION_ENTITY_TYPE);
        this.observe(this.unique, (unique) => this.#entityContext.setUnique(unique ?? null));
        this.observe(this.model, (model) => this.view.setTitle(model?.name), null);

        this.routes.setRoutes([
            {
                path: "create",
                component: UaAutomationWorkspaceEditorElement,
                setup: async () => {
                    await this.scaffold();
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

    protected resetState(): void {
        super.resetState();
        this.#unique.setValue(undefined);
        this.#model.setValue(undefined);
    }

    async scaffold() {
        this.resetState();
        const { data } = await this.#repository.createScaffold();
        if (data) {
            this.#unique.setValue(UA_EMPTY_GUID);
            this.#model.setValue(data);
            this.setIsNew(true);
        }
    }

    async load(id: string) {
        this.resetState();
        const { data, asObservable } = await this.#repository.requestByUnique(id);

        if (asObservable) {
            this.observe(
                asObservable(),
                (model) => {
                    if (model) {
                        this.#unique.setValue(model.unique);
                        this.#model.setValue(structuredClone(model));
                        this.setIsNew(false);
                    }
                },
                "_observeModel",
            );
        }

        return data;
    }

    updateProperty<K extends keyof UaAutomationDetailModel>(key: K, value: UaAutomationDetailModel[K]) {
        const current = this.#model.getValue();
        if (current) {
            this.#model.setValue({ ...current, [key]: value });
        }
    }

    getData(): UaAutomationDetailModel | undefined {
        return this.#model.getValue();
    }

    getUnique(): string | undefined {
        return this.#unique.getValue();
    }

    getEntityType(): string {
        return UA_AUTOMATION_ENTITY_TYPE;
    }

    async submit() {
        const model = this.#model.getValue();
        if (!model) return;

        try {
            await this.#validationContext.validate();
        } catch {
            this.#validationContext.focusFirstInvalidElement();
            return;
        }

        if (this.getIsNew()) {
            const { data, error } = await this.#repository.create(model);
            if (error) throw error;
            if (data) {
                this.#unique.setValue(data.unique);
                this.#model.setValue(data);

                // Reload the root's children so the new item appears in the tree.
                const event = new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
                    unique: null,
                });
                this.#eventContext?.dispatchEvent(event);
            }
        } else {
            const { data, error } = await this.#repository.save(model);
            if (error) throw error;
            if (data) {
                this.#model.setValue(data);

                // Reload this item in the tree (e.g. name changed).
                const event = new UmbRequestReloadStructureForEntityEvent({
                    entityType: UA_AUTOMATION_ENTITY_TYPE,
                    unique: data.unique,
                });
                this.#eventContext?.dispatchEvent(event);
            }
        }

        this.setIsNew(false);
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
