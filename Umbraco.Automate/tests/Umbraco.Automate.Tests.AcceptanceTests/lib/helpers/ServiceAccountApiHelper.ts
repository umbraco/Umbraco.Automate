import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { uniqueSuffix } from './TestData';

/**
 * Service accounts — the execution identity an automation runs as.
 *
 * A service account is a **CMS user of kind Api**, not an Automate entity, so it is created
 * through the CMS user API. This helper is a thin wrapper over `umbracoApi.user` that pins the
 * kind and resolves the user group, because getting either wrong produces a workspace that
 * looks fine until an automation tries to run.
 *
 * The user group decides what the automation is allowed to do. Tests use Administrators so
 * permissions are never the reason a spec fails.
 */
export class ServiceAccountApiHelper {
  api: ApiHelpers;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  /** Resolves a user group id by name. Throws rather than returning null, because every caller
   * needs the id and a missing group is a broken environment, not a test condition. */
  async getUserGroupId(userGroupName: string = 'Administrators'): Promise<string> {
    const userGroup = await this.api.userGroup.getByName(userGroupName);
    if (userGroup === null) {
      throw new Error(`User group "${userGroupName}" was not found.`);
    }

    return userGroup.id;
  }

  /**
   * Creates a service account and returns its key, which is what
   * `CreateWorkspaceRequestModel.serviceAccountKey` expects.
   */
  async create(name: string, userGroupName: string = 'Administrators'): Promise<string> {
    const userGroupId = await this.getUserGroupId(userGroupName);

    // Email must be unique across users, and it is never delivered to — kind Api users cannot
    // log in to the backoffice.
    const email = `automate-service-${uniqueSuffix()}@example.com`;

    return await this.api.user.createDefaultUser(name, email, [userGroupId], 'Api');
  }

  async deleteById(id: string) {
    return await this.api.user.delete(id);
  }

  async ensureNameNotExists(name: string) {
    return await this.api.user.deleteByName(name);
  }
}
