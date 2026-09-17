import { defineConfig, devices } from '@playwright/test';
import * as path from 'path';

require('dotenv').config();

import { umbracoConfig } from './umbraco.config';

export const STORAGE_STATE = path.join(__dirname, 'playwright/.auth/user.json');

/**
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './tests/',
  /* Maximum time one test can run for. */
  timeout: process.env.CI ? 60 * 1000 : 30 * 1000,
  expect: {
    /* Maximum time expect() should wait for the condition to be met. */
    timeout: process.env.CI ? 10_000 : 5_000
  },
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  /* Retry on CI only. Local stays at 0 so a flake is visible while you write specs. */
  retries: process.env.CI ? 2 : 0,
  /* Single worker: specs share the state of one running site. */
  workers: 1,
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: process.env.CI ? [['line'], ['junit', { outputFile: 'results/results.xml' }]] : 'html',
  outputDir: './results',

  use: {
    /* Lets the page object navigate with app-relative paths like
     * `/umbraco/section/automate/...` instead of rebuilding the host every time. */
    baseURL: umbracoConfig.environment.baseUrl,
    actionTimeout: 0,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
    /* The CMS backoffice marks its own chrome with data-mark. Automate's own elements do
     * not carry it, so target those by custom element name instead of getByTestId. */
    testIdAttribute: 'data-mark',
    /* Set SLOW_MO to a number of milliseconds to watch a headed run at human speed, e.g.
     * `SLOW_MO=500 npx playwright test --headed`. Off by default. */
    launchOptions: {
      slowMo: process.env.SLOW_MO ? Number(process.env.SLOW_MO) : 0
    }
  },

  projects: [
    {
      name: 'setup',
      testMatch: '**/*.setup.ts',
    },
    {
      name: 'DefaultConfig',
      testMatch: 'DefaultConfig/**',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        storageState: STORAGE_STATE,
      },
    },
  ],
});
