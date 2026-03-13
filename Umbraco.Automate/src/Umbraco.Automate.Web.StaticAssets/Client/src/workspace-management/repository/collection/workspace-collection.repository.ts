import type { UmbCollectionFilterModel, UmbCollectionRepository } from "@umbraco-cms/backoffice/collection";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UaWorkspaceCollectionServerDataSource } from "./workspace-collection.server.data-source.js";

export class UaWorkspaceCollectionRepository extends UmbRepositoryBase implements UmbCollectionRepository {
    #collectionSource: UaWorkspaceCollectionServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#collectionSource = new UaWorkspaceCollectionServerDataSource(host);
    }

    async requestCollection(filter: UmbCollectionFilterModel) {
        return this.#collectionSource.getCollection(filter);
    }
}

export { UaWorkspaceCollectionRepository as api };
