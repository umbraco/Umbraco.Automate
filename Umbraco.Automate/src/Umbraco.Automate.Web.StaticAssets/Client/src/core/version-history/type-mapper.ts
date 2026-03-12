import type {
    UaVersionComparisonResponse,
    UaVersionHistoryItem,
    UaVersionHistoryResponse,
    UaVersionValueChange,
} from "./types.js";

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type VersionHistoryApiResponse = any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type VersionComparisonApiResponse = any;

export const UaVersionHistoryTypeMapper = {
    mapToVersionHistoryResponse(data: VersionHistoryApiResponse): UaVersionHistoryResponse {
        return {
            currentVersion: data.currentVersion,
            publishedVersion: data.publishedVersion ?? null,
            totalVersions: data.totalVersions,
            versions: data.versions.map(
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                (v: any): UaVersionHistoryItem => ({
                    id: v.id,
                    entityId: v.entityId,
                    version: v.version,
                    dateCreated: v.dateCreated,
                    createdByUserId: v.createdByUserId,
                    createdByUserName: v.createdByUserName,
                    changeDescription: v.changeDescription,
                }),
            ),
        };
    },

    mapToComparisonResponse(data: VersionComparisonApiResponse): UaVersionComparisonResponse {
        return {
            fromVersion: data.fromVersion,
            toVersion: data.toVersion,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            changes: data.changes.map(
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                (c: any): UaVersionValueChange => ({
                    path: c.path,
                    oldValue: c.oldValue,
                    newValue: c.newValue,
                }),
            ),
        };
    },
};
