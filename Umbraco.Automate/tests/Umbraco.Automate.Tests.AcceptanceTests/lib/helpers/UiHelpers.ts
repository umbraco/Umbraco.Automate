import { Page } from '@playwright/test';
import { UiHelpers as UmbracoUiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { ConstantHelper } from './ConstantHelper';
import { AutomateUiHelper } from './AutomateUiHelper';
import { umbracoConfig } from '../../umbraco.config';

export class UiHelpers {
  page: Page;
  umbracoUi: UmbracoUiHelpers;
  automate: AutomateUiHelper;

  constructor(page: Page, umbracoUi: UmbracoUiHelpers) {
    this.page = page;
    this.umbracoUi = umbracoUi;
    this.automate = new AutomateUiHelper(page);
  }

  /**
   * Opens the Automate section by URL.
   *
   * This deliberately does **not** click the section tab. Doing so meant waiting for
   * `section-links`, which never appears when the backoffice is mid re-login — the stored auth
   * state can expire partway through a run, and the page then sits on the login screen until the
   * wait times out. Navigating straight to the section route lets the CMS finish its own auth
   * redirect and land where we asked. Use `expectSectionTabVisible` when a spec genuinely wants
   * to assert the tab is registered.
   */
  async goToAutomateSection() {
    await this.goToUrl(this.automate.sectionUrl());
  }

  /**
   * Navigates, then repairs the session if the backoffice bounced us to the login screen.
   *
   * The storage state saved by `auth.setup.ts` expires partway through a longer run. Specs that
   * touch the management API happen to survive it, because the API helper performs its own
   * re-login and that repairs the shared browser context. A spec that only drives the UI has
   * nothing to trigger that, so it sits on the login page until the wait times out — which is
   * exactly how the Overview dashboard smoke test failed while everything around it passed.
   *
   * Every UI navigation goes through here so that difference cannot bite again.
   */
  async goToUrl(url: string) {
    await this.page.goto(url);
    await this.page.waitForLoadState('domcontentloaded');
    if (await this.reauthenticateIfNeeded()) {
      await this.page.goto(url);
      await this.page.waitForLoadState('domcontentloaded');
    }
  }

  /**
   * Returns true when a re-login was performed.
   *
   * The backoffice answers a navigation with a redirect chain, so the outcome — backoffice
   * chrome or login form — is not decided at `domcontentloaded`. `isVisible()` is immediate and
   * ignores a timeout option, so checking it straight after navigating always said "not on the
   * login screen" and the repair never ran. Race the two outcomes instead, then decide.
   */
  async reauthenticateIfNeeded(): Promise<boolean> {
    const username = this.page.locator('[name="username"]');
    const sectionLinks = this.page.getByTestId('section-links');

    await Promise.race([
      username.waitFor({ state: 'visible', timeout: 30000 }).catch(() => undefined),
      sectionLinks.waitFor({ state: 'visible', timeout: 30000 }).catch(() => undefined)
    ]);

    if (!(await username.isVisible())) {
      return false;
    }

    const { login, password } = umbracoConfig.user;
    if (!login || !password) {
      throw new Error('Session expired and UMBRACO_USER_LOGIN / UMBRACO_USER_PASSWORD are not set.');
    }

    await this.umbracoUi.login.enterEmail(login);
    await this.umbracoUi.login.enterPassword(password);
    await this.umbracoUi.login.clickLoginButton();
    await this.page.getByTestId('section-links').waitFor({ state: 'visible', timeout: 30000 });
    return true;
  }

  /* Asserts the Automate tab is registered in the backoffice nav. Separate from navigation so a
   * slow or re-authenticating backoffice cannot turn this into a navigation failure. */
  async expectSectionTabVisible(timeout: number = 30000) {
    const sectionLinks = this.page.getByTestId('section-links');
    await sectionLinks.waitFor({ state: 'visible', timeout });
    await sectionLinks
      .getByText(ConstantHelper.sections.automate, { exact: true })
      .first()
      .waitFor({ state: 'visible', timeout });
  }
}
