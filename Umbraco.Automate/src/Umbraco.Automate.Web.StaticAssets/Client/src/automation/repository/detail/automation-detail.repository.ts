import { UmbDetailRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRequestReloadChildrenOfEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UaAutomationDetailServerDataSource } from "./automation-detail.server.data-source.js";
import { UA_AUTOMATION_DETAIL_STORE_CONTEXT } from "./automation-detail.store.js";
import type { UaAutomationDetailModel } from "../../types.js";
import { UA_AUTOMATION_ENTITY_TYPE, UA_AUTOMATION_ROOT_ENTITY_TYPE } from "../../constants.js";
import { UaEntityActionEvent, dispatchActionEvent } from "../../../core/index.js";

export class UaAutomationDetailRepository extends UmbDetailRepositoryBase<UaAutomationDetailModel> {
    constructor(host: UmbControllerHost) {
        super(host, UaAutomationDetailServerDataSource, UA_AUTOMATION_DETAIL_STORE_CONTEXT);
    }

    override async create(model: UaAutomationDetailModel) {
        const result = await super.create(model, null);
        if (!result.error && result.data) {
            dispatchActionEvent(this, UaEntityActionEvent.created(result.data.unique, UA_AUTOMATION_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }

    override async save(model: UaAutomationDetailModel) {
        const result = await super.save(model);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.updated(model.unique, UA_AUTOMATION_ENTITY_TYPE));
        }
        return result;
    }

    override async delete(unique: string) {
        const result = await super.delete(unique);
        if (!result.error) {
            dispatchActionEvent(this, UaEntityActionEvent.deleted(unique, UA_AUTOMATION_ENTITY_TYPE));
            dispatchActionEvent(
                this,
                new UmbRequestReloadChildrenOfEntityEvent({
                    entityType: UA_AUTOMATION_ROOT_ENTITY_TYPE,
                    unique: null,
                }),
            );
        }
        return result;
    }
}

export { UaAutomationDetailRepository as api };
