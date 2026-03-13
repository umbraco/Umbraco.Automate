import type {
    WorkspaceResponseModel,
    WorkspaceItemResponseModel,
} from "../api/types.gen.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "./constants.js";
import type { UaWorkspaceDetailModel, UaWorkspaceItemModel } from "./types.js";

export const UaWorkspaceTypeMapper = {
    toDetailModel(response: WorkspaceResponseModel): UaWorkspaceDetailModel {
        return {
            unique: response.id,
            entityType: UA_WORKSPACE_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            serviceAccountKey: response.serviceAccountKey,
            userGroups: response.userGroups,
            allowedConnections: response.allowedConnections,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toItemModel(response: WorkspaceItemResponseModel): UaWorkspaceItemModel {
        return {
            unique: response.id,
            entityType: UA_WORKSPACE_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            serviceAccountKey: response.serviceAccountKey,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toCreateRequest(model: UaWorkspaceDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            serviceAccountKey: model.serviceAccountKey,
            userGroups: model.userGroups,
            allowedConnections: model.allowedConnections,
        };
    },

    toUpdateRequest(model: UaWorkspaceDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            serviceAccountKey: model.serviceAccountKey,
            userGroups: model.userGroups,
            allowedConnections: model.allowedConnections,
        };
    },
};
