import { Page } from '@playwright/test';
import { ApiHelpers as UmbracoApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { AutomationApiHelper } from './AutomationApiHelper';
import { CatalogueApiHelper } from './CatalogueApiHelper';
import { ConnectionApiHelper } from './ConnectionApiHelper';
import { ContentApiHelper } from './ContentApiHelper';
import { ServiceAccountApiHelper } from './ServiceAccountApiHelper';
import { WorkspaceApiHelper } from './WorkspaceApiHelper';

/**
 * Façade over the Automate-only management API helpers, mirroring how `umbracoApi` groups the
 * CMS ones. Reuses the injected CMS ApiHelpers so both share one authenticated request context.
 */
export class ApiHelpers {
  page: Page;
  umbracoApi: UmbracoApiHelpers;
  automations: AutomationApiHelper;
  catalogue: CatalogueApiHelper;
  connections: ConnectionApiHelper;
  content: ContentApiHelper;
  workspaces: WorkspaceApiHelper;
  serviceAccounts: ServiceAccountApiHelper;

  constructor(page: Page, umbracoApi: UmbracoApiHelpers) {
    this.page = page;
    this.umbracoApi = umbracoApi;
    this.automations = new AutomationApiHelper(umbracoApi);
    this.catalogue = new CatalogueApiHelper(umbracoApi);
    this.connections = new ConnectionApiHelper(umbracoApi);
    this.content = new ContentApiHelper(umbracoApi);
    this.workspaces = new WorkspaceApiHelper(umbracoApi);
    this.serviceAccounts = new ServiceAccountApiHelper(umbracoApi);
  }
}
