import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';
import { AutomationApiHelper } from './AutomationApiHelper';
import { toAlias, uniqueName } from './TestData';

/** A workspace created by a fixture, and everything a spec needs to use it. */
export type TestWorkspace = {
  id: string;
  name: string;
  alias: string;
  /** `ConstantHelper.emptyGuid` for a tier-1 workspace. */
  serviceAccountKey: string;
};

export type CreateWorkspaceOptions = {
  alias?: string;
  /** Defaults to the empty Guid — accepted, but gives automations no execution identity. */
  serviceAccountKey?: string;
  /** Defaults to empty. Admins bypass membership, so tests rarely need this. */
  userGroups?: string[];
  allowedConnections?: string[];
};

/**
 * Automate workspaces, via the Automate management API.
 *
 * A workspace is the container every automation belongs to, and the Automate sidebar menu is
 * gated on at least one existing (`UA_WORKSPACES_EXIST_CONDITION_ALIAS`), so most UI specs need
 * one before they can do anything.
 *
 * Create and delete both require an **admin** backoffice user. The test user is admin, so this
 * works out of the box — but a spec that switches user will start getting 403s here.
 */
export class WorkspaceApiHelper {
  api: ApiHelpers;
  basePath: string = ConstantHelper.api.basePath;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  async getAll() {
    const requestUrl = this.api.baseUrl + this.basePath + 'workspaces';
    const response = await this.api.get(requestUrl);
    const body = await response.json();
    return body.items ?? [];
  }

  async getByName(name: string) {
    const items = await this.getAll();
    return items.find((item: any) => item.name === name) ?? null;
  }

  async existsByName(name: string) {
    return (await this.getByName(name)) !== null;
  }

  /* Returns the first workspace on the site, or null when none exist. Use this to skip a spec
   * that needs a workspace rather than failing it with an unrelated UI error. */
  async getFirst() {
    const items = await this.getAll();
    return items.length > 0 ? items[0] : null;
  }

  /** Creates a workspace and returns its id. */
  async create(name: string, options: CreateWorkspaceOptions = {}): Promise<string> {
    const requestUrl = this.api.baseUrl + this.basePath + 'workspaces';
    const response = await this.api.post(requestUrl, {
      alias: options.alias ?? toAlias(name),
      name,
      serviceAccountKey: options.serviceAccountKey ?? ConstantHelper.emptyGuid,
      userGroups: options.userGroups ?? [],
      allowedConnections: options.allowedConnections ?? []
    });

    // The endpoint returns 201 with both a Location header and the new id as the body. Prefer
    // the header, but fall back to the body so a routing change to CreatedAtAction does not
    // silently break every fixture.
    const location = response.headers()['location'];
    if (location) {
      return location.replace(/\/+$/, '').split('/').pop()!;
    }

    return (await response.text()).trim().replace(/^"|"$/g, '');
  }

  /**
   * Creates a uniquely-named workspace for a single test run.
   *
   * The name and alias carry a random suffix because the server does **not** enforce alias
   * uniqueness on workspaces — a fixed alias would quietly pile up duplicates whenever a run
   * died before teardown.
   */
  async createForTest(namePrefix: string, options: CreateWorkspaceOptions = {}): Promise<TestWorkspace> {
    const name = uniqueName(namePrefix);
    const alias = options.alias ?? toAlias(name);
    const serviceAccountKey = options.serviceAccountKey ?? ConstantHelper.emptyGuid;

    const id = await this.create(name, { ...options, alias, serviceAccountKey });

    return { id, name, alias, serviceAccountKey };
  }

  async deleteById(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'workspaces/' + id;
    return await this.api.delete(requestUrl);
  }

  /**
   * Removes a workspace and everything a test put in it.
   *
   * Automations are deleted first on purpose: it is not confirmed that deleting a workspace
   * cascades to its automations, and an orphaned automation would leak into later specs. Drop
   * the first step once the cascade is verified.
   */
  async cleanUp(id: string) {
    const automations = new AutomationApiHelper(this.api);
    await automations.deleteAllInWorkspace(id);
    await this.deleteById(id);
  }
}
