import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";
import type {
    ActionLogLevelModel,
    AutomationRunStatusModel,
    StepRunStatusModel,
} from "../api/types.gen.js";

export interface UaStepRunLogEntryModel {
    timestampUtc: string;
    level: ActionLogLevelModel;
    message: string;
}

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
    logEntries: UaStepRunLogEntryModel[];
}

/**
 * A recorded run payload prepared for display by the server: pretty-printed JSON with
 * sensitive values masked, or `null` when nothing was recorded.
 */
export interface UaRunDataValueModel {
    value: string | null;
    truncated: boolean;
}

export interface UaStepRunDataModel {
    input: UaRunDataValueModel;
    output: UaRunDataValueModel;
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
    /** The alias of the trigger that started the run, from the automation version that ran. */
    triggerAlias: string | null;
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
    error: string | null;
}

export interface UaRunSummary {
    totalRuns: number;
    byStatus: Record<string, number>;
}
