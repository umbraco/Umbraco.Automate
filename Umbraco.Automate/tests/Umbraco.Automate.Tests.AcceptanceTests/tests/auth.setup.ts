import { test as setup } from '@playwright/test';
import { STORAGE_STATE } from '../playwright.config';
import { ConstantHelper, UiHelpers } from '@umbraco-cms/acceptance-test-helpers';
import { umbracoConfig } from '../umbraco.config';

setup('authenticate', async ({ page }) => {
  const { login, password } = umbracoConfig.user;
  if (!login || !password) {
    throw new Error('UMBRACO_USER_LOGIN and UMBRACO_USER_PASSWORD are not set. Run "npm run config".');
  }

  const umbracoUi = new UiHelpers(page);

  await umbracoUi.goToBackOffice();

  // If an external auth provider is configured, the username/password form is behind a button.
  const authProviderButton = page.getByRole('button', { name: 'Sign in with Umbraco', exact: true });
  try {
    await authProviderButton.waitFor({ state: 'visible', timeout: 10000 });
    await authProviderButton.click();
  } catch {
    // No external provider screen — the login form is shown directly.
  }

  await page.locator('[name="username"]').waitFor({ state: 'visible', timeout: 30000 });
  await umbracoUi.login.enterEmail(login);
  await umbracoUi.login.enterPassword(password);
  await umbracoUi.login.clickLoginButton();

  await page.getByTestId('section-links').waitFor({ state: 'visible', timeout: 30000 });
  await umbracoUi.login.goToSection(ConstantHelper.sections.settings);
  await umbracoUi.page.context().storageState({ path: STORAGE_STATE });
});
