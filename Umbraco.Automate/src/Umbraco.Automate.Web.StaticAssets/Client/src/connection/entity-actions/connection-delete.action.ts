import { UaDeleteActionBase, type UaDeleteActionArgs } from "../../core/index.js";
import { UaConnectionDetailRepository } from "../repository/detail/connection-detail.repository.js";

export class UaConnectionDeleteAction extends UaDeleteActionBase {
    protected getArgs(): UaDeleteActionArgs {
        return {
            headline: "#actions_delete",
            confirmMessage: "#uaConnection_deleteConfirm",
            getRepository: (host) => new UaConnectionDetailRepository(host),
        };
    }
}

export { UaConnectionDeleteAction as api };
