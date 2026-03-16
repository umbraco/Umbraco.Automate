import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbUniqueTreeStore } from "@umbraco-cms/backoffice/tree";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import { UA_WORKSPACE_MGMT_TREE_STORE_ALIAS } from "./constants.js";

export class UaWorkspaceMgmtTreeStore extends UmbUniqueTreeStore {
    constructor(host: UmbControllerHost) {
        super(host, UA_WORKSPACE_MGMT_TREE_STORE_ALIAS);
    }
}

export default UaWorkspaceMgmtTreeStore;

export const UA_WORKSPACE_MGMT_TREE_STORE_CONTEXT = new UmbContextToken<UaWorkspaceMgmtTreeStore>(
    UA_WORKSPACE_MGMT_TREE_STORE_ALIAS,
);
