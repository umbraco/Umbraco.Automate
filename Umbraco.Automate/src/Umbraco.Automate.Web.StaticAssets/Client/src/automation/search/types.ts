import type { UmbSearchResultItemModel } from "@umbraco-cms/backoffice/search";
import type { AutomationStatusModel, AutomationHealthModel } from "../../api/types.gen.js";

export interface UaAutomationSearchItemModel extends UmbSearchResultItemModel {
    status: AutomationStatusModel;
    health: AutomationHealthModel;
}
