import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { uniqueName } from './TestData';

export type TestPage = { id: string; name: string };
export type TestMedia = { id: string; name: string };

/**
 * Content and media for specs whose automations point at real CMS items (pick a node, publish it,
 * read a property from it).
 *
 * Everything is created under a unique name and removed again by `cleanUp()`, which the
 * `cmsContent` fixture runs after the test, so a spec never depends on what the demo site
 * happens to hold. Everything CMS-side goes through `umbracoApi`; Automate's own API has no
 * equivalent.
 */
export class CmsContentApiHelper {
  /** The alias of the one property on the test page type (the Textstring data type's alias). */
  static readonly pagePropertyAlias = 'textstring';

  private readonly api: ApiHelpers;
  private documentTypeName: string | null = null;
  private documentTypeId: string | null = null;
  private readonly documentIds: string[] = [];
  private readonly mediaIds: string[] = [];

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  /**
   * The key of a content type that is allowed at the root and has one Textstring property
   * (`pagePropertyAlias`). Created on first use and shared by every page of this test.
   */
  async pageTypeId(): Promise<string> {
    if (this.documentTypeId !== null) {
      return this.documentTypeId;
    }

    const dataType = await this.api.dataType.getByName('Textstring');
    this.documentTypeName = uniqueName('Automate Test Page');
    this.documentTypeId = await this.api.documentType.createDocumentTypeWithPropertyEditor(
      this.documentTypeName,
      'Textstring',
      dataType.id
    );
    return this.documentTypeId;
  }

  /** A root page holding `text` in its property. Published when `publish` is true, because the
   * read actions (Get Content, Get Content Property) work off the published cache. */
  async createPage(namePrefix: string, options: { text?: string; publish?: boolean } = {}): Promise<TestPage> {
    const name = uniqueName(namePrefix);
    const typeId = await this.pageTypeId();
    const id = await this.api.document.createDocumentWithTextContent(name, typeId, options.text ?? 'text', 'Textstring');
    this.documentIds.push(id);

    if (options.publish) {
      await this.api.document.publish(id);
      await this.api.document.waitUntilDocumentIsPublished(id);
    }

    return { id, name };
  }

  async createFolder(namePrefix: string, parentId?: string): Promise<TestMedia> {
    const name = uniqueName(namePrefix);
    const id = parentId
      ? await this.api.media.createDefaultMediaFolderAndParentId(name, parentId)
      : await this.api.media.createDefaultMediaFolder(name);
    this.mediaIds.push(id);
    return { id, name };
  }

  async createImage(namePrefix: string, parentId?: string): Promise<TestMedia> {
    const name = uniqueName(namePrefix);
    const id = parentId
      ? await this.api.media.createDefaultMediaWithImageAndParentId(name, parentId)
      : await this.api.media.createDefaultMediaWithImage(name);
    this.mediaIds.push(id);
    return { id, name };
  }

  /**
   * Takes over a page an automation created, by name, so `cleanUp()` removes it too. Returns its
   * id, or null when there is no such page.
   */
  async adoptPageByName(name: string): Promise<string | null> {
    const page = await this.api.document.getByName(name);
    if (!page) {
      return null;
    }
    this.documentIds.push(page.id);
    return page.id;
  }

  /** Whether a root-level content node with this name exists, for "nothing was created" checks. */
  async rootPageExists(name: string): Promise<boolean> {
    return Boolean(await this.api.document.doesNameExist(name));
  }

  /**
   * Removes everything this helper created, whether it is live or in the recycle bin.
   *
   * Each item is removed from both places, because a spec may have trashed it, and failures are
   * ignored: an item the spec already deleted is not an error here.
   */
  async cleanUp() {
    const base = `${this.api.baseUrl}/umbraco/management/api/v1`;
    for (const id of this.documentIds) {
      await this.api.document.delete(id);
      await this.api.delete(`${base}/recycle-bin/document/${id}`);
    }
    for (const id of this.mediaIds) {
      await this.api.media.delete(id);
      await this.api.delete(`${base}/recycle-bin/media/${id}`);
    }
    if (this.documentTypeName !== null) {
      await this.api.documentType.ensureNameNotExists(this.documentTypeName);
    }
  }
}
