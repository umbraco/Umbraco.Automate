import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";
import type {
    AutomationStatusModel,
    TriggerConfigurationModel,
    StepConfigurationModel,
    StepConnectionModel,
} from "../api/types.gen.js";

export interface UaAutomationDetailModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    description: string | null;
    isEnabled: boolean;
    status: AutomationStatusModel;
    publishedVersion: number | null;
    draftVersion: number;
    trigger: TriggerConfigurationModel | null;
    steps: StepConfigurationModel[];
    connections: StepConnectionModel[];
    canvasState: string | null;
    version: number;
    dateCreated: string;
    dateModified: string;
}

export interface UaAutomationItemModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    description: string | null;
    isEnabled: boolean;
    status: AutomationStatusModel;
    version: number;
    dateCreated: string;
    dateModified: string;
}
