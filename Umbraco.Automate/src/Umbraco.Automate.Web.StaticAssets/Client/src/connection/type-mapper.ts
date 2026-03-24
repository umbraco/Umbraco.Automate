import type {
    ConnectionResponseModel,
    ConnectionItemResponseModel,
} from "../api/types.gen.js";
import { UA_CONNECTION_ENTITY_TYPE } from "./constants.js";
import type { UaConnectionDetailModel, UaConnectionItemModel } from "./types.js";

export const UaConnectionTypeMapper = {
    toDetailModel(response: ConnectionResponseModel): UaConnectionDetailModel {
        return {
            unique: response.id,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            type: response.type,
            settings: response.settings,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toItemModel(response: ConnectionItemResponseModel): UaConnectionItemModel {
        return {
            unique: response.id,
            entityType: UA_CONNECTION_ENTITY_TYPE,
            alias: response.alias,
            name: response.name,
            type: response.type,
            version: response.version,
            dateCreated: response.dateCreated,
            dateModified: response.dateModified,
        };
    },

    toCreateRequest(model: UaConnectionDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            type: model.type,
            settings: model.settings,
            version: model.version,
        };
    },

    toUpdateRequest(model: UaConnectionDetailModel) {
        return {
            alias: model.alias,
            name: model.name,
            type: model.type,
            settings: model.settings,
            version: model.version,
        };
    },
};
