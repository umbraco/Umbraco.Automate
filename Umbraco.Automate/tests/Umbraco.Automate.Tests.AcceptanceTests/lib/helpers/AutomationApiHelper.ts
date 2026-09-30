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

  /* The full automation detail — steps, connections, status. */
  async getById(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id;
    const response = await this.api.get(requestUrl);
    return await response.json();
  }

  /* Publishes through the API. Throws with the server's problem detail on a validation failure,
   * so a spec that seeds a published automation fails on the real reason. */
  async publish(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id + '/publish';
    const response = await this.api.post(requestUrl, {});
    if (!response.ok()) {
      throw new Error(`Publishing automation ${id} failed (${response.status()}): ${await response.text()}`);
    }
  }

  /* Starts a run, as Run now does. The automation must be published and have a manual trigger. */
  async run(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id + '/trigger';
    const response = await this.api.post(requestUrl, {});
    if (!response.ok()) {
      throw new Error(`Running automation ${id} failed (${response.status()}): ${await response.text()}`);
    }
  }

  /* The runs of one automation, newest first. */
  async getRuns(id: string): Promise<any[]> {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id + '/runs';
    const response = await this.api.get(requestUrl);
    const body = await response.json();
    return body.items ?? [];
  }

  /* One run, including its per-step runs. The runs listing leaves `stepRuns` empty. */
  async getRun(runId: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'runs/' + runId;
    const response = await this.api.get(requestUrl);
    return await response.json();
  }

  /**
   * Finds a step in an automation detail by its alias.
   *
   * Seeded steps must be looked up this way: the server assigns its own step ids on create, so
   * the id a spec generated is not the id it gets back.
   */
  stepByAlias(automation: any, alias: string) {
    const step = automation.steps.find((s: any) => s.alias === alias);
    if (!step) {
      throw new Error(`Step "${alias}" not found. Steps: ${automation.steps.map((s: any) => s.alias).join(', ')}`);
    }
    return step;
  }

  /* The steps that were not in `before` — i.e. what the UI just added. */
  addedSteps(before: any, after: any): any[] {
    const known = new Set(before.steps.map((s: any) => s.id));
    return after.steps.filter((s: any) => !known.has(s.id));
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
