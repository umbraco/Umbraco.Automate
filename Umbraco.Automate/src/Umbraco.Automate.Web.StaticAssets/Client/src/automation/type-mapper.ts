import type {
    AutomationResponseModel,
    AutomationItemResponseModel,
} from "../api/types.gen.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "./constants.js";
import type { UaAutomationDetailModel, UaAutomationItemModel } from "./types.js";

export const UaAutomationTypeMapper = {
    toDetailModel(response: AutomationResponseModel): UaAutomationDetailModel {
        return {
            unique: response.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            isEnabled: response.isEnabled,
            workspaceId: response.workspaceId,
            groupId: response.groupId ?? null,
            status: response.status,
            publishedVersion: response.publishedVersion ?? null,
            draftVersion: response.version,
            trigger: response.trigger ?? null,
            steps: response.steps,
            connections: response.connections,
            canvasState: response.canvasState ?? null,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toItemModel(response: AutomationItemResponseModel): UaAutomationItemModel {
        return {
            unique: response.id,
            entityType: UA_AUTOMATION_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            description: response.description ?? null,
            isEnabled: response.isEnabled,
            status: response.status,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toCreateRequest(model: UaAutomationDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            description: model.description,
            workspaceId: model.workspaceId,
            groupId: model.groupId,
            isEnabled: model.isEnabled,
            trigger: model.trigger,
            steps: model.steps,
            connections: model.connections,
            canvasState: model.canvasState,
            version: model.version,
        };
    },

    toUpdateRequest(model: UaAutomationDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            description: model.description,
            isEnabled: model.isEnabled,
            trigger: model.trigger,
            steps: model.steps,
            connections: model.connections,
            canvasState: model.canvasState,
            version: model.version,
        };
    },
};
