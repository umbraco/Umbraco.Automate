import { UmbDetailRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRequestReloadChildrenOfEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UaWorkspaceDetailServerDataSource } from "./workspace-detail.server.data-source.js";
import { UA_WORKSPACE_DETAIL_STORE_CONTEXT } from "./workspace-detail.store.js";
import type { UaWorkspaceDetailModel } from "../../types.js";
import { UA_WORKSPACE_ENTITY_TYPE, UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../../constants.js";
import { UaEntityActionEvent, dispatchActionEvent } from "../../../core/index.js";

export class UaWorkspaceDetailRepository extends UmbDetailRepositoryBase<UaWorkspaceDetailModel> {
    constructor(host: UmbControllerHost) {
        super(host, UaWorkspaceDetailServerDataSource, UA_WORKSPACE_DETAIL_STORE_CONTEXT);
    }

    override async create(model: UaWorkspaceDetailModel) {
        const result = await super.create(model, null);
        if (!result.error && result.data) {
            dispatchActionEvent(this, UaEntityActionEvent.created(result.data.unique, UA_WORKSPACE_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }

    override async save(model: UaWorkspaceDetailModel) {
        const result = await super.save(model);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.updated(model.unique, UA_WORKSPACE_ENTITY_TYPE));
        }
        return result;
    }

    override async delete(unique: string) {
        const result = await super.delete(unique);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.deleted(unique, UA_WORKSPACE_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }
}

export { UaWorkspaceDetailRepository as api };
