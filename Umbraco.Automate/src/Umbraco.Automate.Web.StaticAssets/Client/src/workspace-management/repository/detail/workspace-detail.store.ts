import { UmbDetailStoreBase } from "@umbraco-cms/backoffice/store";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UaWorkspaceDetailModel } from "../../types.js";

export const UA_WORKSPACE_DETAIL_STORE_CONTEXT = new UmbContextToken<UaWorkspaceDetailStore>(
    "UaWorkspaceDetailStore",
);

export class UaWorkspaceDetailStore extends UmbDetailStoreBase<UaWorkspaceDetailModel> {
    constructor(host: UmbControllerHost) {
        super(host, UA_WORKSPACE_DETAIL_STORE_CONTEXT.toString());
    }
}

export { UaWorkspaceDetailStore as api };
