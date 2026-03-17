import { UmbDetailStoreBase } from "@umbraco-cms/backoffice/store";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { UaConnectionDetailModel } from "../../types.js";

export const UA_CONNECTION_DETAIL_STORE_CONTEXT = new UmbContextToken<UaConnectionDetailStore>(
    "UaConnectionDetailStore",
);

export class UaConnectionDetailStore extends UmbDetailStoreBase<UaConnectionDetailModel> {
    constructor(host: UmbControllerHost) {
        super(host, UA_CONNECTION_DETAIL_STORE_CONTEXT.toString());
    }
}

export { UaConnectionDetailStore as api };
