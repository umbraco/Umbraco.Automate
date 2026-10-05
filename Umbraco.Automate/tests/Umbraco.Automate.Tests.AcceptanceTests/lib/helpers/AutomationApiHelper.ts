import { expect } from '@playwright/test';
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
  /** e.g. `{ channels: [{ channelAlias: 'umbracoAutomate.webhook', settings: {…}, isEnabled: true, notifyOn: 'Failed' }] }`.
   * Left out of the body when not given, so the server applies its own default. */
  notificationSettings?: { channels: unknown[] };
};

/* The run lifecycle endpoints under `runs/{id}/…`, as the run detail modal's footer calls them. */
export type RunLifecycleAction = 'resume' | 'suspend' | 'terminate';

export type ApprovalOutcome = 'Approved' | 'Rejected';

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
      connections: options.connections ?? [],
      notificationSettings: options.notificationSettings
    });
    if (!response.ok()) {
      throw new Error(`Creating automation "${name}" failed (${response.status()}): ${await response.text()}`);
    }

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

  /* Renames through the API, as another editor saving would. The save bumps the automation's
   * version, so an editor that loaded it earlier now holds a stale copy. */
  async rename(id: string, name: string) {
    const current = await this.getById(id);
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id;
    const response = await this.api.put(requestUrl, {
      alias: current.alias,
      name,
      description: current.description,
      groupId: current.groupId,
      trigger: current.trigger,
      steps: current.steps,
      connections: current.connections,
      canvasState: current.canvasState,
      notificationSettings: current.notificationSettings,
      version: current.version
    });
    if (!response.ok()) {
      throw new Error(`Renaming automation ${id} failed (${response.status()}): ${await response.text()}`);
    }
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

  /* Unpublishes through the API, as the Unpublish workspace action does. */
  async unpublish(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'automations/' + id + '/unpublish';
    const response = await this.api.post(requestUrl, {});
    if (!response.ok()) {
      throw new Error(`Unpublishing automation ${id} failed (${response.status()}): ${await response.text()}`);
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
   * Waits until one run reaches `status`, and returns its detail.
   *
   * Runs execute in the background, so every run assertion starts by polling; the timeout is
   * generous because a run is picked up by the engine's poll loop, not straight away.
   */
  async waitForRunStatus(runId: string, status: string, timeout: number = 30000) {
    await expect.poll(async () => (await this.getRun(runId)).status, { timeout }).toBe(status);
    return await this.getRun(runId);
  }

  /**
   * Publishes and runs an automation, waits for its newest run to reach `status`, and returns
   * that run's id.
   *
   * The newest run is read from the listing (newest first) rather than from the trigger call,
   * because `trigger` answers before the run record exists and does not return its id.
   */
  async publishAndRunUntil(automationId: string, status: string, timeout: number = 30000): Promise<string> {
    await this.publish(automationId);
    await this.run(automationId);

    let runId = '';
    await expect
      .poll(
        async () => {
          const [newest] = await this.getRuns(automationId);
          runId = newest?.id ?? '';
          return newest?.status;
        },
        { timeout }
      )
      .toBe(status);
    return runId;
  }

  /**
   * Publishes and runs an automation that pauses on a Request Approval step, and returns the run
   * id once it is parked there. A run waiting on an approval is `Suspended`, with the approval
   * step `WaitingForInput`.
   */
  async startWaitingRun(automationId: string): Promise<string> {
    return await this.publishAndRunUntil(automationId, 'Suspended');
  }

  /**
   * Resumes, suspends or terminates a run, and returns the raw response.
   *
   * Deliberately does not throw: refusals are behaviour worth asserting on (resuming a run that
   * waits on an approval answers 409), so the caller decides what a non-2xx means.
   */
  async postRunLifecycle(runId: string, action: RunLifecycleAction) {
    const requestUrl = this.api.baseUrl + this.basePath + 'runs/' + runId + '/' + action;
    return await this.api.post(requestUrl, {});
  }

  /**
   * The pending approvals of one automation, from `approvals/pending`.
   *
   * The endpoint lists every pending approval on the site, and the demo site may hold other
   * automations' approvals, so always scope to the automation the spec created.
   */
  async getPendingApprovals(automationId: string): Promise<any[]> {
    const requestUrl = this.api.baseUrl + this.basePath + 'approvals/pending';
    const response = await this.api.get(requestUrl);
    const items = await response.json();
    return (items ?? []).filter((item: any) => item.automationId === automationId);
  }

  /* Decides a pending approval, as the decision modal does. Throws with the server's problem
   * detail on failure, so cleanup that relies on it fails loudly rather than leaving a run waiting. */
  async decideApproval(runId: string, stepId: string, outcome: ApprovalOutcome) {
    const requestUrl = this.api.baseUrl + this.basePath + 'approvals/' + runId + '/steps/' + stepId + '/decision';
    const response = await this.api.post(requestUrl, { outcome });
    if (!response.ok()) {
      throw new Error(`Deciding approval ${runId}/${stepId} failed (${response.status()}): ${await response.text()}`);
    }
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
