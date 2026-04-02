import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import {
    UmbWorkspaceRouteManager,
    type UmbRoutableWorkspaceContext,
} from "@umbraco-cms/backoffice/workspace";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import { UmbEntityContext, type UmbEntityModel } from "@umbraco-cms/backoffice/entity";
import { UmbBasicState, UmbObjectState } from "@umbraco-cms/backoffice/observable-api";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UmbWorkspaceEditorElement } from "@umbraco-cms/backoffice/workspace";

/**
 * A minimal routable workspace context that sets up edit/:unique routing
 * and exposes entity type + unique via UmbEntityContext.
 *
 * Satisfies UMB_SUBMITTABLE_TREE_ENTITY_WORKSPACE_CONTEXT discriminator
 * so menu structure contexts and breadcrumbs can resolve correctly.
 *
 * Use this for workspaces that only need to resolve their entity identity
 * (e.g. folder/group workspaces, read-only workspace views).
 */
export class UaRoutableWorkspaceContext extends UmbContextBase implements UmbRoutableWorkspaceContext {
    public readonly workspaceAlias: string;
    public readonly routes = new UmbWorkspaceRouteManager(this);
    public readonly view = new UmbViewContext(this, null);

    #nameState = new UmbBasicState<string>("");
    public readonly name = this.#nameState.asObservable();

    #entityContext = new UmbEntityContext(this);
    public readonly unique = this.#entityContext.unique;
    public readonly entityType = this.#entityContext.entityType;

    #isNew = new UmbBasicState<boolean | undefined>(false);
    public readonly isNew = this.#isNew.asObservable();

    #createUnderParent = new UmbObjectState<UmbEntityModel | undefined>(undefined);
    // eslint-disable-next-line @typescript-eslint/naming-convention
    public readonly _internal_createUnderParent = this.#createUnderParent.asObservable();
    // eslint-disable-next-line @typescript-eslint/naming-convention
    public readonly _internal_createUnderParentEntityType = this.#createUnderParent.asObservablePart(
        (p) => p?.entityType,
    );
    // eslint-disable-next-line @typescript-eslint/naming-convention
    public readonly _internal_createUnderParentEntityUnique = this.#createUnderParent.asObservablePart(
        (p) => p?.unique,
    );

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
                    this.onRouteSetup(unique);
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

    getIsNew() {
        return false;
    }

    async requestSubmit() {
        // Read-only workspace — no submit needed
    }

    // eslint-disable-next-line @typescript-eslint/naming-convention
    _internal_getCreateUnderParent() {
        return undefined;
    }

    // eslint-disable-next-line @typescript-eslint/naming-convention
    _internal_setCreateUnderParent(_parent: UmbEntityModel | undefined) {
        // No-op
    }

    protected setName(name: string | undefined) {
        this.#nameState.setValue(name ?? "");
        this.view.setTitle(name);
    }

    /**
     * Override to perform additional setup when the route resolves a unique.
     * Called after the entity unique has been set.
     */
    protected onRouteSetup(_unique: string): void {}
}
