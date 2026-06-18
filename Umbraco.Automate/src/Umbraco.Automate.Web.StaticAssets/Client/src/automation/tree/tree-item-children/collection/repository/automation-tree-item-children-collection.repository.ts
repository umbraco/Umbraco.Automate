import { UaWorkspaceTreeRepository } from "../../../../../workspace-management/tree/workspace-tree.repository.js";
import type { UmbCollectionFilterModel, UmbCollectionRepository } from "@umbraco-cms/backoffice/collection";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UMB_ENTITY_CONTEXT, type UmbEntityModel } from "@umbraco-cms/backoffice/entity";

export class UaAutomationTreeItemChildrenCollectionRepository
    extends UmbRepositoryBase
    implements UmbCollectionRepository
{
    #treeRepository = new UaWorkspaceTreeRepository(this);

    async requestCollection(filter: UmbCollectionFilterModel) {
        const entityContext = await this.getContext(UMB_ENTITY_CONTEXT);
        if (!entityContext) throw new Error("Entity context not found");

        const entityType = entityContext.getEntityType();
        const unique = entityContext.getUnique();

        if (!entityType) throw new Error("Entity type not found");
        if (unique === undefined) throw new Error("Unique not found");

        const parent: UmbEntityModel = { entityType, unique };

        const paging = { skip: filter.skip ?? 0, take: filter.take ?? 100 };

        if (parent.unique === null) {
            return this.#treeRepository.requestTreeRootItems({ paging });
        } else {
            return this.#treeRepository.requestTreeItemsOf({ parent, paging });
        }
    }
}

export { UaAutomationTreeItemChildrenCollectionRepository as api };
