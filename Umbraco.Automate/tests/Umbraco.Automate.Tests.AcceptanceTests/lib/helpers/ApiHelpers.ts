import { Page } from '@playwright/test';
import { ApiHelpers as UmbracoApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { AutomationApiHelper } from './AutomationApiHelper';
import { ConnectionApiHelper } from './ConnectionApiHelper';
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
  connections: ConnectionApiHelper;
  workspaces: WorkspaceApiHelper;
  serviceAccounts: ServiceAccountApiHelper;

  constructor(page: Page, umbracoApi: UmbracoApiHelpers) {
    this.page = page;
    this.umbracoApi = umbracoApi;
    this.automations = new AutomationApiHelper(umbracoApi);
    this.connections = new ConnectionApiHelper(umbracoApi);
    this.workspaces = new WorkspaceApiHelper(umbracoApi);
    this.serviceAccounts = new ServiceAccountApiHelper(umbracoApi);
  }
}
