import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UaRoutableWorkspaceContext } from "../../../core/index.js";
import { UA_WORKSPACE_VIEW_WORKSPACE_ALIAS } from "../constants.js";
import { UA_WORKSPACE_ENTITY_TYPE } from "../../constants.js";
import { UaWorkspaceDetailRepository } from "../../repository/detail/workspace-detail.repository.js";

export class UaWorkspaceViewWorkspaceContext extends UaRoutableWorkspaceContext {
    #detailRepository: UaWorkspaceDetailRepository;

    constructor(host: UmbControllerHost) {
        super(host, UA_WORKSPACE_VIEW_WORKSPACE_ALIAS, UA_WORKSPACE_ENTITY_TYPE);
        this.#detailRepository = new UaWorkspaceDetailRepository(this);
    }

    override async onRouteSetup(unique: string) {
        const { asObservable } = await this.#detailRepository.requestByUnique(unique);

        this.observe(
            asObservable?.(),
            (detail) => this.view.setTitle(detail?.name),
            null,
        );
    }
}

export { UaWorkspaceViewWorkspaceContext as api };
