import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './recording',
  testMatch: 'workflow.spec.ts',
  outputDir: '../artifacts/e2e/playwright-recording',
  workers: 1,
  timeout: 90_000,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../artifacts/e2e/report', open: 'never' }],
    ['json', { outputFile: '../artifacts/e2e/results.json' }],
  ],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    viewport: { width: 1440, height: 1120 },
    video: { mode: 'on', size: { width: 1440, height: 1120 } },
    trace: 'on',
    launchOptions: { slowMo: 250 },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
