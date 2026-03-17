import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";
import type {
    AutomationRunStatusModel,
    StepRunStatusModel,
} from "../api/types.gen.js";

export interface UaStepRunModel {
    id: string;
    stepId: string;
    actionAlias: string;
    status: StepRunStatusModel;
    startedUtc: string | null;
    completedUtc: string | null;
    error: string | null;
    retryCount: number;
    durationMs: number | null;
}

export interface UaRunDetailModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    automationId: string;
    automationVersion: number;
    status: AutomationRunStatusModel;
    startedUtc: string | null;
    completedUtc: string | null;
    initiatedBy: string;
    correlationId: string | null;
    error: string | null;
    stepRuns: UaStepRunModel[];
}

export interface UaRunItemModel {
    unique: string;
    automationId: string;
    automationName?: string;
    status: AutomationRunStatusModel;
    startedUtc: string | null;
    completedUtc: string | null;
    initiatedBy: string;
    durationMs: number | null;
}
