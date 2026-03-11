import type { UaCatalogueItemModel, UaCatalogueMode } from "../../types.js";

export interface UaNodePickerModalData {
    mode: UaCatalogueMode;
}

export interface UaNodePickerModalValue {
    item: UaCatalogueItemModel;
}
