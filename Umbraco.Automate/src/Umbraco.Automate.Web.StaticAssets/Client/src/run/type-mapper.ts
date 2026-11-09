import type {
    AutomationRunListItemResponseModel,
    AutomationRunResponseModel,
    RunTriggerDataResponseModel,
    StepRunDataResponseModel,
} from "../api/types.gen.js";
import { UA_RUN_ENTITY_TYPE } from "./constants.js";
import type {
    UaRunDataValueModel,
    UaRunDetailModel,
    UaRunItemModel,
    UaStepRunDataModel,
    UaStepRunModel,
} from "./types.js";

export const UaRunTypeMapper = {
    toStepRunDataModel(response: StepRunDataResponseModel): UaStepRunDataModel {
        return {
            input: { value: response.input ?? null, truncated: response.inputTruncated },
            output: { value: response.output ?? null, truncated: response.outputTruncated },
        };
    },

    toTriggerDataModel(response: RunTriggerDataResponseModel): UaRunDataValueModel {
        return { value: response.triggerData ?? null, truncated: response.triggerDataTruncated };
    },

    toDetailModel(response: AutomationRunResponseModel): UaRunDetailModel {
        return {
            unique: response.id,
            entityType: UA_RUN_ENTITY_TYPE,
            automationId: response.automationId,
            automationVersion: response.automationVersion,
            status: response.status,
            startedUtc: response.startedUtc ?? null,
            completedUtc: response.completedUtc ?? null,
            initiatedBy: response.initiatedBy,
            correlationId: response.correlationId ?? null,
            error: response.error ?? null,
            triggerAlias: response.triggerAlias ?? null,
            stepRuns: response.stepRuns.map(
                (sr): UaStepRunModel => ({
                    id: sr.id,
                    stepId: sr.stepId,
                    actionAlias: sr.actionAlias,
                    status: sr.status,
                    startedUtc: sr.startedUtc ?? null,
                    completedUtc: sr.completedUtc ?? null,
                    error: sr.error ?? null,
                    retryCount: sr.retryCount,
                    durationMs: sr.durationMs ?? null,
                    branchOutcome: sr.branchOutcome ?? null,
                    logEntries: sr.logEntries.map((entry) => ({
                        timestampUtc: entry.timestampUtc,
                        level: entry.level,
                        message: entry.message,
                    })),
                }),
            ),
        };
    },

    toItemModel(response: AutomationRunResponseModel): UaRunItemModel {
        const startMs = response.startedUtc ? new Date(response.startedUtc).getTime() : null;
        const endMs = response.completedUtc ? new Date(response.completedUtc).getTime() : null;
        const durationMs = startMs != null && endMs != null ? endMs - startMs : null;

        return {
            unique: response.id,
            automationId: response.automationId,
            status: response.status,
            startedUtc: response.startedUtc ?? null,
            completedUtc: response.completedUtc ?? null,
            initiatedBy: response.initiatedBy,
            durationMs,
            error: response.error ?? null,
        };
    },

    toListItemModel(response: AutomationRunListItemResponseModel): UaRunItemModel {
        const startMs = response.startedUtc ? new Date(response.startedUtc).getTime() : null;
        const endMs = response.completedUtc ? new Date(response.completedUtc).getTime() : null;
        const durationMs = startMs != null && endMs != null ? endMs - startMs : null;

        return {
            unique: response.id,
            automationId: response.automationId,
            automationName: response.automationName,
            status: response.status,
            startedUtc: response.startedUtc ?? null,
            completedUtc: response.completedUtc ?? null,
            initiatedBy: response.initiatedBy,
            durationMs,
            error: response.error ?? null,
        };
    },
};
