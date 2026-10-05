import { ApiHelpers } from '@umbraco-cms/acceptance-test-helpers';

/**
 * Read-only lookups of content that already exists on the demo site.
 *
 * Specs that run Get Content need a real content key. They must not publish new content to get
 * one: on the SQLite demo site a content publish can be followed by an unrelated table lock that
 * stalls every later spec. Create-and-delete of content is the CMS helpers' job (`umbracoApi.document`);
 * this only finds what is already there.
 */
export class ContentApiHelper {
  api: ApiHelpers;

  constructor(api: ApiHelpers) {
    this.api = api;
  }

  /** A published, non-trashed document at the root of the content tree. */
  async getPublishedRootDocument(): Promise<{ key: string; name: string }> {
    const requestUrl = this.api.baseUrl + '/umbraco/management/api/v1/tree/document/root?skip=0&take=50';
    const response = await this.api.get(requestUrl);
    const body = await response.json();
    const published = (body.items ?? []).find(
      (item: any) => !item.isTrashed && item.variants?.some((v: any) => v.state === 'Published')
    );
    if (!published) {
      throw new Error('The demo site has no published content at the root to run against.');
    }
    return { key: published.id, name: published.variants.find((v: any) => v.state === 'Published').name };
  }

  /** The key of a published, non-trashed document at the root of the content tree. */
  async getPublishedRootDocumentKey(): Promise<string> {
    return (await this.getPublishedRootDocument()).key;
  }
}
