import { expect } from '@playwright/test';
import {
  ApiHelpers as UmbracoApiHelpers,
  ConstantHelper as CmsConstantHelper,
  ElementBuilder,
  UserGroupBuilder
} from '@umbraco-cms/acceptance-test-helpers';
import type { ApiHelpers } from './ApiHelpers';
import { ensureOk } from './ApiResponse';
import { uniqueName, uniqueSuffix } from './TestData';
import type { TestWorkspace } from './WorkspaceApiHelper';

/**
 * Library elements, folders, element types, pages and service-account workspaces for specs whose
 * automations point at real Library items. The Library counterpart of `CmsContentApiHelper`.
 *
 * Everything is created under a unique name and removed again by `cleanUp()`, which the `library`
 * fixture runs after the test, live or in the Library recycle bin. Every element type has one
 * Textstring property, whose alias (`propertyAlias`) stands in for the stories' `customSchema`.
 * Everything CMS-side goes through the CMS helpers, which throw on a failed call.
 */
export class LibraryApiHelper {
  /** The one property every element type here has: the Textstring data type's alias. */
  static readonly propertyAlias = 'textstring';

  private readonly api: UmbracoApiHelpers;
  private get managementApi() {
    return `${this.api.baseUrl}/umbraco/management/api/v1`;
  }
  private readonly automate: ApiHelpers;
  private defaultElementTypeId: string | null = null;
  private readonly elementIds: string[] = [];
  private readonly folderIds: string[] = [];
  private readonly documentIds: string[] = [];
  private readonly documentTypeNames: string[] = [];
  private readonly templateNames: string[] = [];
  private readonly dataTypeNames: string[] = [];
  private readonly userGroupIds: string[] = [];
  private readonly serviceAccountKeys: string[] = [];
  private readonly workspaceIds: string[] = [];

  constructor(api: UmbracoApiHelpers, automate: ApiHelpers) {
    this.api = api;
    this.automate = automate;
  }

  /** An element type allowed in the Library, with one Textstring property. */
  async elementType(namePrefix: string = 'Schema snippet'): Promise<{ id: string; name: string }> {
    const dataType = await this.api.dataType.getByName('Textstring');
    const name = uniqueName(namePrefix);
    const id = await this.api.documentType.createDefaultElementType(name, 'Content', 'Textstring', dataType.id);
    this.documentTypeNames.push(name);
    return { id, name };
  }

  /** A document type that is not an element type, for "element types only" checks. */
  async pageType(namePrefix: string = 'Not an element type'): Promise<{ id: string; name: string }> {
    const name = uniqueName(namePrefix);
    const id = await this.api.documentType.createDefaultDocumentType(name);
    this.documentTypeNames.push(name);
    return { id, name };
  }

  async folder(namePrefix: string = 'Reviews', parentId?: string): Promise<{ id: string; name: string }> {
    const name = uniqueName(namePrefix);
    const id = await this.api.element.createDefaultElementFolder(name, parentId);
    this.folderIds.push(id);
    return { id, name };
  }

  /** An element holding `text` in its property, at the root or in `folderId`. Published when asked. */
  async element(
    namePrefix: string,
    options: { typeId?: string; folderId?: string; text?: string; publish?: boolean } = {}
  ): Promise<{ id: string; name: string }> {
    const name = uniqueName(namePrefix);
    const typeId = options.typeId ?? (await this.defaultTypeId());
    const builder = new ElementBuilder().withDocumentTypeId(typeId);
    if (options.folderId) {
      builder.withParentId(options.folderId);
    }
    const id = await this.api.element.create(
      builder
        .addVariant()
        .withName(name)
        .done()
        .addValue()
        .withAlias(LibraryApiHelper.propertyAlias)
        .withValue(options.text ?? 'text')
        .done()
        .build()
    );
    this.elementIds.push(id);

    if (options.publish) {
      await this.api.element.publish(id);
      await expect.poll(async () => await this.api.element.isElementPublished(id)).toBe(true);
    }

    return { id, name };
  }

  /**
   * Saves an element as an editor would: reads it, changes its Textstring value and PUTs it back
   * through the CMS Management API.
   */
  async save(id: string, text: string = `saved-${uniqueSuffix()}`) {
    const element = await this.api.element.get(id);
    const value = element.values.find((v: any) => v.alias === LibraryApiHelper.propertyAlias);
    if (!value) {
      throw new Error(`Element ${id} has no "${LibraryApiHelper.propertyAlias}" value.`);
    }
    value.value = text;
    await ensureOk(await this.api.element.update(id, element), `Saving element ${id}`);
  }

  /** Publishes an element as an editor would, and waits until it reads as published. */
  async publish(id: string) {
    const status = await this.api.element.publish(id);
    if (status >= 300) {
      throw new Error(`Publishing element ${id} failed (${status}).`);
    }
    await expect.poll(async () => await this.api.element.isElementPublished(id)).toBe(true);
  }

  /** Unpublishes an element through `PUT element/{id}/unpublish`. The CMS helpers have no unpublish. */
  async unpublish(id: string) {
    await ensureOk(await this.api.put(`${this.managementApi}/element/${id}/unpublish`, { cultures: null }), `Unpublishing element ${id}`);
    await expect.poll(async () => await this.api.element.isElementPublished(id)).toBe(false);
  }

  /** Moves an element to a Library folder through `PUT element/{id}/move`. The CMS helpers move folders only. */
  async move(id: string, folderId: string) {
    await ensureOk(await this.api.put(`${this.managementApi}/element/${id}/move`, { target: { id: folderId } }), `Moving element ${id} to folder ${folderId}`);
  }

  /** Restores an element from the Library recycle bin through `PUT recycle-bin/element/{id}/restore`, to the root or `targetFolderId`. */
  async restore(id: string, targetFolderId?: string) {
    const target = targetFolderId ? { id: targetFolderId } : null;
    await ensureOk(await this.api.put(`${this.managementApi}/recycle-bin/element/${id}/restore`, { target }), `Restoring element ${id}`);
  }

  /** The saved (draft) value of an element's Textstring property. */
  async propertyValue(id: string): Promise<unknown> {
    const element = await this.api.element.get(id);
    return element.values.find((v: any) => v.alias === LibraryApiHelper.propertyAlias)?.value;
  }

  /** Takes over an element an automation created, by name, so it is removed too. Returns it, or null. */
  async adoptByName(name: string): Promise<any | null> {
    const element = await this.api.element.getByName(name);
    if (!element) {
      return null;
    }
    this.elementIds.push(element.id);
    return element;
  }

  /** The names of the elements and folders directly inside a folder. */
  async childNames(folderId: string): Promise<string[]> {
    return (await this.api.element.getChildren(folderId)).map((child: any) => child.name);
  }

  /** Moves an element, or a folder, to the Library recycle bin. `cleanUp()` still removes it from there. */
  async trash(id: string, isFolder: boolean = false) {
    const status = await this.api.element.moveToRecycleBin(id, isFolder);
    if (status >= 300) {
      throw new Error(`Moving ${isFolder ? 'folder' : 'element'} ${id} to the recycle bin failed (${status}).`);
    }
  }

  /** A user group with the Library section, element root access and every element permission it needs. */
  static userGroup(name: string = uniqueName('Automate Library Group')): UserGroupBuilder {
    return new UserGroupBuilder()
      .withName(name)
      .addSection(CmsConstantHelper.sectionAliases.library)
      .withElementRootAccess(true)
      .addFallbackPermission()
      .withReadElementPermission(true)
      .withCreateElementPermission(true)
      .withUpdateElementPermission(true)
      .withPublishElementPermission(true)
      .withUnpublishElementPermission(true)
      .withReadElementContainerPermission(true)
      .done();
  }

  /**
   * A workspace whose service account is in a new user group built from `group`, so a spec
   * controls its sections, Library start node and element permissions.
   */
  async workspaceFor(group: UserGroupBuilder): Promise<TestWorkspace> {
    const payload = group.build();
    const userGroupId = await this.api.userGroup.create(payload);
    this.userGroupIds.push(userGroupId);
    const serviceAccountKey = await this.automate.serviceAccounts.create(uniqueName('Library Service Account'), payload.name);
    this.serviceAccountKeys.push(serviceAccountKey);
    const workspace = await this.automate.workspaces.createForTest('Library Workspace', {
      serviceAccountKey,
      userGroups: [userGroupId]
    });
    this.workspaceIds.push(workspace.id);
    return workspace;
  }

  /** A workspace whose service account has the Library section with `startNodeId` as its Library start node. */
  async workspaceStartingAt(startNodeId: string): Promise<TestWorkspace> {
    const group = new UserGroupBuilder()
      .withName(uniqueName('Automate Library Scoped Group'))
      .addSection(CmsConstantHelper.sectionAliases.library)
      .withElementRootAccess(false)
      .withElementStartNodeId(startNodeId)
      .addFallbackPermission()
      .withReadElementPermission(true)
      .withCreateElementPermission(true)
      .withUpdateElementPermission(true)
      .withPublishElementPermission(true)
      .withUnpublishElementPermission(true)
      .withReadElementContainerPermission(true)
      .done();
    return await this.workspaceFor(group);
  }

  /**
   * A published page whose element picker picks `elementId`, rendered by a template that prints
   * each picked element's Textstring value. Returns the page's URL.
   *
   * The plan names `createTemplateWithDisplayingElementPickerExtensionMethods`, but that template
   * prints each element's name and identity, never a property value, so it could not show a value
   * Update Element Property wrote. `createTemplateWithDisplayingValue` wraps the same loop around
   * `element.Value(...)` instead.
   */
  async pageShowing(elementId: string): Promise<string> {
    const propertyName = 'Element';
    const pickerName = uniqueName('Automate Element Picker');
    const pickerId = await this.api.dataType.createDefaultElementPickerDataType(pickerName);
    this.dataTypeNames.push(pickerName);

    const templateName = uniqueName('Automate Element Page');
    const templateId = await this.api.template.createTemplateWithDisplayingValue(
      templateName,
      '\n@using Umbraco.Cms.Core.Models.PublishedContent' +
        '\n@{' +
        '\n\tvar elements = Model.Value<IEnumerable<IPublishedElement>>("element");' +
        '\n}' +
        '\n@if (elements != null)' +
        '\n{' +
        '\n\tforeach (var element in elements)' +
        '\n\t{' +
        `\n\t\t<p>@element.Value("${LibraryApiHelper.propertyAlias}")</p>` +
        '\n\t}' +
        '\n}'
    );
    this.templateNames.push(templateName);

    const pageTypeName = uniqueName('Automate Element Page Type');
    const pageTypeId = await this.api.documentType.createDocumentTypeWithPropertyEditorAndAllowedTemplate(
      pageTypeName,
      pickerId,
      propertyName,
      templateId
    );
    this.documentTypeNames.push(pageTypeName);

    const pageId = await this.api.document.createDocumentWithElementPickers(uniqueName('Element Page'), pageTypeId, propertyName, [
      elementId
    ]);
    this.documentIds.push(pageId);
    await this.api.document.publish(pageId);
    await this.api.document.waitUntilDocumentIsPublished(pageId);
    return await this.api.document.getDocumentUrl(pageId);
  }

  /** Removes everything this helper created. Failures are ignored: a spec may already have removed it. */
  async cleanUp() {
    for (const id of this.workspaceIds) {
      await this.automate.workspaces.cleanUp(id);
    }
    for (const key of this.serviceAccountKeys) {
      await this.automate.serviceAccounts.deleteById(key);
    }
    for (const id of this.userGroupIds) {
      await this.api.userGroup.delete(id);
    }

    const recycleBin = `${this.managementApi}/recycle-bin/element`;
    for (const id of this.documentIds) {
      await this.api.document.delete(id);
    }
    for (const id of this.elementIds) {
      await this.api.element.delete(id);
      await this.api.delete(`${recycleBin}/${id}`);
    }
    for (const id of [...this.folderIds].reverse()) {
      await this.api.element.deleteFolder(id);
      await this.api.delete(`${recycleBin}/folder/${id}`);
    }
    for (const name of this.documentTypeNames) {
      await this.api.documentType.ensureNameNotExists(name);
    }
    for (const name of this.templateNames) {
      await this.api.template.ensureNameNotExists(name);
    }
    for (const name of this.dataTypeNames) {
      await this.api.dataType.ensureNameNotExists(name);
    }
  }

  private async defaultTypeId(): Promise<string> {
    if (this.defaultElementTypeId === null) {
      this.defaultElementTypeId = (await this.elementType()).id;
    }
    return this.defaultElementTypeId;
  }
}
