/**
 * Represents a single version history item.
 */
export interface UaVersionHistoryItem {
    /** The unique identifier of this version record. */
    id: string;
    /** The ID of the entity this version belongs to. */
    entityId: string;
    /** The version number (1, 2, 3, etc.). */
    version: number;
    /** The date and time when this version was created. */
    dateCreated: string;
    /** The user ID who created this version, if available. */
    createdByUserId?: string | null;
    /** Optional description of what changed in this version. */
    changeDescription?: string | null;
    /** Whether this version is the currently published version. */
    isPublished: boolean;
}

/**
 * Response model for version history with pagination info.
 */
export interface UaVersionHistoryResponse {
    /** The current version of the entity. */
    currentVersion: number;
    /** The published version of the entity, if applicable. */
    publishedVersion?: number | null;
    /** Total number of versions. */
    totalVersions: number;
    /** The versions in this page. */
    versions: UaVersionHistoryItem[];
}

/**
 * Represents a value change between two versions.
 */
export interface UaVersionValueChange {
    /** The path of the value that changed. */
    path: string;
    /** The old value (from the source version). */
    oldValue?: string | null;
    /** The new value (from the target version). */
    newValue?: string | null;
}

/**
 * Response model for version comparison.
 */
export interface UaVersionComparisonResponse {
    /** The source version number. */
    fromVersion: number;
    /** The target version number. */
    toVersion: number;
    /** The list of value changes. */
    changes: UaVersionValueChange[];
}
