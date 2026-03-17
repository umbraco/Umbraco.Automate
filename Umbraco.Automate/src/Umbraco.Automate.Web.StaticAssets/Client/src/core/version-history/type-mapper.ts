import type {
    EntityVersionComparisonResponseModel,
    EntityVersionHistoryResponseModel,
    EntityVersionResponseModel,
    ValueChangeModel,
} from "../../api/index.js";
import type {
    UaVersionComparisonResponse,
    UaVersionHistoryItem,
    UaVersionHistoryResponse,
    UaVersionValueChange,
} from "./types.js";

export const UaVersionHistoryTypeMapper = {
    mapToVersionHistoryResponse(data: EntityVersionHistoryResponseModel): UaVersionHistoryResponse {
        return {
            currentVersion: data.currentVersion,
            publishedVersion: data.publishedVersion ?? null,
            totalVersions: data.totalVersions,
            versions: data.versions.map(
                (v: EntityVersionResponseModel): UaVersionHistoryItem => ({
                    id: v.id,
                    entityId: v.entityId,
                    version: v.version,
                    dateCreated: v.dateCreated,
                    createdByUserId: v.createdByUserId,
                    changeDescription: v.changeDescription,
                    isPublished: v.isPublished,
                }),
            ),
        };
    },

    mapToComparisonResponse(data: EntityVersionComparisonResponseModel): UaVersionComparisonResponse {
        return {
            fromVersion: data.fromVersion,
            toVersion: data.toVersion,
            changes: data.changes.map(
                (c: ValueChangeModel): UaVersionValueChange => ({
                    path: c.path,
                    oldValue: c.oldValue,
                    newValue: c.newValue,
                }),
            ),
        };
    },
};
