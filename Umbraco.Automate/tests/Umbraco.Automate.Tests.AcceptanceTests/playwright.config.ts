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
  /* On CI, stop once this many tests have failed. When the site wedges, every remaining spec fails
   * in fixture setup, and three attempts each would otherwise run the job into its timeout. */
  maxFailures: process.env.CI ? 10 : undefined,
  /* Single worker: specs share the state of one running site. */
  workers: 1,
  /* With one worker this does not run anything concurrently; it lets --shard split the suite by
   * test rather than by file, so CI shards finish together instead of waiting on the slowest file.
   * Safe because no spec shares state between its tests (no beforeAll, serial mode or
   * worker-scoped fixtures) — keep it that way, or mark such a file test.describe.configure({ mode: 'serial' }). */
  fullyParallel: true,
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: process.env.CI ? [['line'], ['junit', { outputFile: 'results/results.xml' }]] : 'html',
  outputDir: './results',

  use: {
    /* Lets the page object navigate with app-relative paths like
     * `/umbraco/section/automation/...` instead of rebuilding the host every time. */
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
