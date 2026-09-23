import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';

/**
 * Connections — named, reusable credential sets for external services.
 *
 * Which connection **types** exist depends on which provider packages are installed. The demo
 * site ships Umbraco.Automate.Slack, so `slack` is the only type available, and it is normally
 * unconfigured (no client id/secret in appsettings), which leaves "Authenticate with Slack"
 * disabled. A connection record can still be created, renamed and deleted without credentials —
 * that is what the specs cover.
 */
export class ConnectionApiHelper {
  api: ApiHelpers;
  basePath: string = ConstantHelper.api.basePath;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  async getAll() {
    const requestUrl = this.api.baseUrl + this.basePath + 'connections';
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

  async getFullByName(name: string) {
    const item = await this.getByName(name);
    if (item === null) {
      return null;
    }

    const requestUrl = this.api.baseUrl + this.basePath + 'connections/' + item.id;
    const response = await this.api.get(requestUrl);
    return await response.json();
  }

  async deleteById(id: string) {
    const requestUrl = this.api.baseUrl + this.basePath + 'connections/' + id;
    return await this.api.delete(requestUrl);
  }

  async ensureNameNotExists(name: string) {
    const item = await this.getByName(name);
    if (item !== null) {
      await this.deleteById(item.id);
    }
  }
}
