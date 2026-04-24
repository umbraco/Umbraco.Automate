import { UmbWorkspaceActionBase } from "@umbraco-cms/backoffice/workspace";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLocalizationController } from "@umbraco-cms/backoffice/localization-api";
import { ConnectionsService } from "../../../../api/sdk.gen.js";
import { UA_CONNECTION_WORKSPACE_CONTEXT } from "../connection-workspace.context-token.js";

export class UaConnectionTestAction extends UmbWorkspaceActionBase {
    #localize = new UmbLocalizationController(this);

    async execute() {
        const context = await this.getContext(UA_CONNECTION_WORKSPACE_CONTEXT);
        if (!context) return;

        const unique = context.getUnique();
        if (!unique) return;

        const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);

        const { data, error } = await ConnectionsService.postConnectionsByIdTest({
            path: { id: unique },
        });

        if (error || !data) {
            // Transport-level failure (404, 500, network) — the connection type never
            // ran its check. Show a generic error so the user retries rather than
            // assuming the credentials are actually broken.
            notifications?.peek("danger", {
                data: {
                    headline: this.#localize.term("uaConnection_testFailure"),
                    message: this.#localize.term("uaConnection_testError"),
                },
            });
            return;
        }

        const message = [data.message, ...(data.details ?? [])].filter(Boolean).join("\n");

        switch (data.status) {
            case "Success":
                notifications?.peek("positive", {
                    data: {
                        headline: this.#localize.term("uaConnection_testSuccess"),
                        message,
                    },
                });
                return;
            case "Warning":
                notifications?.peek("warning", {
                    data: {
                        headline: this.#localize.term("uaConnection_testWarning"),
                        message,
                    },
                });
                return;
            default:
                notifications?.peek("danger", {
                    data: {
                        headline: this.#localize.term("uaConnection_testFailure"),
                        message,
                    },
                });
        }
    }
}

export { UaConnectionTestAction as api };
