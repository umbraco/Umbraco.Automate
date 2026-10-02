import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { RunsService } from "../../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../../type-mapper.js";

export class UaRunDetailServerDataSource {
    #host: UmbControllerHost;

    constructor(host: UmbControllerHost) {
        this.#host = host;
    }

    async read(unique: string) {
        const { data, error } = await tryExecute(
            this.#host,
            RunsService.getRunsById({ path: { id: unique } }),
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaRunTypeMapper.toDetailModel(data) };
    }

    /**
     * Reads a step run's recorded input and output. Notifications are disabled: the caller
     * shows a failure inline in the expanded step rather than as a toast.
     */
    async readStepRunData(runUnique: string, stepRunId: string) {
        const { data, error } = await tryExecute(
            this.#host,
            RunsService.getRunsByIdStepRunsByStepRunIdData({ path: { id: runUnique, stepRunId } }),
            { disableNotifications: true },
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaRunTypeMapper.toStepRunDataModel(data) };
    }

    /**
     * Reads a run's recorded trigger data. Notifications are disabled: the caller shows a
     * failure inline rather than as a toast.
     */
    async readTriggerData(runUnique: string) {
        const { data, error } = await tryExecute(
            this.#host,
            RunsService.getRunsByIdTriggerData({ path: { id: runUnique } }),
            { disableNotifications: true },
        );

        if (error || !data) {
            return { error };
        }

        return { data: UaRunTypeMapper.toTriggerDataModel(data) };
    }
}
