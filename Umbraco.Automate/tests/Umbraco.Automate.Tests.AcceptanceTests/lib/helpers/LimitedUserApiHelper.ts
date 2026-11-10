import { randomUUID } from 'crypto';
import { request } from '@playwright/test';
import { ApiHelpers, ConstantHelper as UmbracoConstantHelper } from '@umbraco-cms/acceptance-test-helpers';
import { ensureOk } from './ApiResponse';

/**
 * Calls an endpoint as a backoffice user in a given user group, for specs that assert a refusal.
 *
 * There is one fixed user per group, reused on every call and never deleted: the CMS refuses to
 * delete a user who has logged in, so a user created per call would pile up. Each call resets the
 * user's password to a random value, and puts its group and state right if something changed them.
 *
 * The user signs in through a separate request context, so the admin cookie on the shared
 * `umbracoApi` context is left alone and the spec can keep using it afterwards.
 */
export class LimitedUserApiHelper {
  api: ApiHelpers;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  /** Returns the HTTP status of a GET to `url`, made as the fixed user of `userGroupName`. */
  async getStatusAs(userGroupName: string, url: string): Promise<number> {
    const userGroup = await this.api.userGroup.getByName(userGroupName);
    if (userGroup === null) {
      throw new Error(`User group "${userGroupName}" was not found.`);
    }

    const name = `Automate Limited User (${userGroupName})`;
    const email = `automate-limited-${userGroupName.toLowerCase().replace(/[^a-z0-9]+/g, '-')}@example.com`;
    const userId = await this.ensureUser(name, email, userGroup.id);

    // Random, and not derived from the email, so the account cannot be signed in to by guessing.
    const password = `${randomUUID()}-Aa1!`;
    await ensureOk(await this.api.user.updatePassword(userId, password), 'Setting the limited user password');

    const context = await request.newContext({ ignoreHTTPSErrors: true });
    try {
      const login = await context.post(this.api.baseUrl + UmbracoConstantHelper.apiEndpoints.backOfficeLogin, {
        headers: { 'Content-Type': 'application/json', Referer: this.api.baseUrl, Origin: this.api.baseUrl },
        data: { username: email, password }
      });
      await ensureOk(login, `Signing in ${email}`);

      const response = await context.get(url);
      return response.status();
    } finally {
      await context.dispose();
    }
  }

  /** Finds the user, or creates it, and makes sure it is enabled and in exactly `userGroupId`. */
  private async ensureUser(name: string, email: string, userGroupId: string): Promise<string> {
    const user = await this.api.user.getByName(name);
    if (user === null) {
      return await this.api.user.createDefaultUser(name, email, [userGroupId]);
    }

    const groupIds: string[] = (user.userGroupIds ?? []).map((group: { id: string }) => group.id);
    if (groupIds.length !== 1 || groupIds[0] !== userGroupId) {
      await ensureOk(await this.api.user.setUserGroups([user.id], [userGroupId]), `Moving ${email} to its group`);
    }
    if (user.state === 'Disabled') {
      await ensureOk(await this.api.user.enable([user.id]), `Enabling ${email}`);
    }
    if (user.state === 'LockedOut') {
      await ensureOk(await this.api.user.unlock([user.id]), `Unlocking ${email}`);
    }

    return user.id;
  }
}
