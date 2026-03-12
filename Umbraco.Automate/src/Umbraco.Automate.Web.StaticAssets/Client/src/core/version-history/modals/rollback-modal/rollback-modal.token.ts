import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaVersionValueChange } from "../../types.js";

/**
 * Data passed to the rollback modal.
 */
export interface UaRollbackModalData {
    /** The source version being compared (the version to rollback to). */
    fromVersion: number;
    /** The target version being compared (usually the current version). */
    toVersion: number;
    /** The list of property changes between the versions. */
    changes: UaVersionValueChange[];
}

/**
 * Value returned from the rollback modal.
 */
export interface UaRollbackModalValue {
    /** Whether the user confirmed the rollback. */
    rollback: boolean;
}

/**
 * Modal token for the rollback confirmation modal.
 */
export const UA_ROLLBACK_MODAL = new UmbModalToken<UaRollbackModalData, UaRollbackModalValue>(
    "UmbracoAutomate.Modal.Rollback",
    {
        modal: {
            type: "sidebar",
            size: "medium",
        },
    },
);
