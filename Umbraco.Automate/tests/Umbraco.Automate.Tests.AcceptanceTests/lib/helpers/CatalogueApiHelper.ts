import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';

/**
 * The catalogue — which triggers, actions, control flows and connection types the site has
 * installed, with their settings and output schemas.
 *
 * Specs read expected text from here (an action's name, an output property's description)
 * rather than hard-coding copy that a provider package owns and may reword.
 */
export class CatalogueApiHelper {
  api: ApiHelpers;
  basePath: string = ConstantHelper.api.basePath + 'catalogue/';

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  private async getList(path: string): Promise<any[]> {
    const response = await this.api.get(this.api.baseUrl + this.basePath + path);
    const body = await response.json();
    return Array.isArray(body) ? body : body.items ?? [];
  }

  async getActions() {
    return await this.getList('actions');
  }

  async getControlFlows() {
    return await this.getList('control-flows');
  }

  async getConnectionTypes() {
    return await this.getList('connection-types');
  }

  /**
   * The display name of an action or control flow — what the action picker lists it as. Specs
   * choose picker entries by this rather than a hard-coded label.
   */
  async getStepTypeName(alias: string): Promise<string> {
    const all = [...(await this.getActions()), ...(await this.getControlFlows())];
    const item = all.find((i: any) => i.alias === alias);
    if (!item) {
      throw new Error(`Step type "${alias}" is not in the catalogue.`);
    }
    return item.name;
  }

  async getConnectionTypeName(alias: string): Promise<string> {
    const type = (await this.getConnectionTypes()).find((t: any) => t.alias === alias);
    if (!type) {
      throw new Error(`Connection type "${alias}" is not installed.`);
    }
    return type.name;
  }

  async getActionByAlias(alias: string) {
    const action = (await this.getActions()).find((a: any) => a.alias === alias);
    if (!action) {
      throw new Error(`Action "${alias}" is not in the catalogue.`);
    }
    return action;
  }

  /* The description of one output property of an action, from its JSON Schema. */
  async getOutputDescription(actionAlias: string, property: string): Promise<string> {
    const action = await this.getActionByAlias(actionAlias);
    const description = action.outputSchema?.properties?.[property]?.description;
    if (!description) {
      throw new Error(`Output "${property}" of "${actionAlias}" has no description.`);
    }
    return description;
  }

  /* The description of one settings field of an action, as the settings form receives it. */
  async getFieldDescription(actionAlias: string, fieldKey: string): Promise<string> {
    const action = await this.getActionByAlias(actionAlias);
    const description = action.settingsSchema?.fields?.find((f: any) => f.key === fieldKey)?.description;
    if (!description) {
      throw new Error(`Setting "${fieldKey}" of "${actionAlias}" has no description.`);
    }
    return description;
  }
}
