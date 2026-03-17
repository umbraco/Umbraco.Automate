import { UmbDetailRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRequestReloadChildrenOfEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UaConnectionDetailServerDataSource } from "./connection-detail.server.data-source.js";
import { UA_CONNECTION_DETAIL_STORE_CONTEXT } from "./connection-detail.store.js";
import type { UaConnectionDetailModel } from "../../types.js";
import { UA_CONNECTION_ENTITY_TYPE, UA_CONNECTION_ROOT_ENTITY_TYPE } from "../../constants.js";
import { UaEntityActionEvent, dispatchActionEvent } from "../../../core/index.js";

export class UaConnectionDetailRepository extends UmbDetailRepositoryBase<UaConnectionDetailModel> {
    constructor(host: UmbControllerHost) {
        super(host, UaConnectionDetailServerDataSource, UA_CONNECTION_DETAIL_STORE_CONTEXT);
    }

    override async create(model: UaConnectionDetailModel) {
        const result = await super.create(model, null);
        if (!result.error && result.data) {
            dispatchActionEvent(this, UaEntityActionEvent.created(result.data.unique, UA_CONNECTION_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_CONNECTION_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }

    override async save(model: UaConnectionDetailModel) {
        const result = await super.save(model);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.updated(model.unique, UA_CONNECTION_ENTITY_TYPE));
        }
        return result;
    }

    override async delete(unique: string) {
        const result = await super.delete(unique);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.deleted(unique, UA_CONNECTION_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_CONNECTION_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }
}

export { UaConnectionDetailRepository as api };
