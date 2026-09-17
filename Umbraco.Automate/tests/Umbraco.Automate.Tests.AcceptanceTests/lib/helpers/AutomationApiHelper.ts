import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';
import { toAlias } from './TestData';

export type CreateAutomationOptions = {
  alias?: string;
  description?: string | null;
  groupId?: string | null;
  /** e.g. `{ triggerAlias: 'manualTrigger', settings: {} }`. Required before an automation can publish. */
  trigger?: { triggerAlias: string; settings: Record<string, unknown> } | null;
  steps?: unknown[];
  connections?: unknown[];
};

/**
 * Automations, via the Automate management API.
 *
 * There is no CMS equivalent for these endpoints, so this helper is the Automate counterpart of
 * Forms' FormsApiHelper: everything CMS-side (documents, doc types, users) still goes through
 * `umbracoApi.*`.
 */
export class AutomationApiHelper {
  api: ApiHelpers;
  basePath: string = ConstantHelper.api.basePath;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  async getAll() {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations';
    const response = await this.api.get(requestUrl);
    const body = await response.json();
    return body.items ?? [];
  }

  /* Returns the list item for the named automation, or null. */
  async getByName(name: string) {
    const items = await this.getAll();
    return items.find((item: any) => item.name === name) ?? null;
  }

  async existsByName(name: string) {
    return (await this.getByName(name)) !== null;
  }

  /* Returns the full automation detail (the GET automations/{id} body), or null if the name
   * does not exist. Assert on individual properties from this rather than collapsing several
   * checks into one boolean — a failure then points at the exact field. */
  async getFullByName(name: string) {
    const item = await this.getByName(name);
    if (item === null) {
      return null;
    }

    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + item.id;
    const response = await this.api.get(requestUrl);
    return await response.json();
  }

  /**
   * Creates an automation and returns its id.
   *
   * Seeds a spec that is testing something other than creation. A automation created this way
   * has no trigger and no steps, so it saves fine but will fail validation on publish.
   */
  async create(name: string, workspaceId: string, options: CreateAutomationOptions = {}): Promise<string> {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations';
    const response = await this.api.post(requestUrl, {
      alias: options.alias ?? toAlias(name),
      name,
      description: options.description ?? null,
      workspaceId,
      groupId: options.groupId ?? null,
      trigger: options.trigger ?? null,
      steps: options.steps ?? [],
      connections: options.connections ?? []
    });

    const location = response.headers()['location'];
    if (location) {
      return location.replace(/\/+$/, '').split('/').pop()!;
    }

    return (await response.text()).trim().replace(/^"|"$/g, '');
  }

  async deleteById(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id;
    return await this.api.delete(requestUrl);
  }

  async ensureNameNotExists(name: string) {
    const item = await this.getByName(name);
    if (item !== null) {
      await this.deleteById(item.id);
    }
  }

  /* Used by workspace teardown, because it is not confirmed that deleting a workspace cascades
   * to its automations. */
  async deleteAllInWorkspace(workspaceId: string) {
    const items = await this.getAll();
    for (const item of items.filter((i: any) => i.workspaceId === workspaceId)) {
      await this.deleteById(item.id);
    }
  }
}
