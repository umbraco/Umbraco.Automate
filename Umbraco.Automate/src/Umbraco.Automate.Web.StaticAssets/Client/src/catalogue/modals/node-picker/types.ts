import type { UaCatalogueItemModel, UaCatalogueMode } from "../../types.js";

export interface UaNodePickerModalData {
    mode: UaCatalogueMode;
    /** When provided, actions requiring a connection type not available in the workspace are hidden. */
    workspaceId?: string;
}

export interface UaNodePickerModalValue {
    item: UaCatalogueItemModel;
}
