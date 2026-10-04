import { defineConfig } from '@playwright/test';
import recording from './playwright.recording.config';

// Explicit opt-in smoke test: requires running services and a working provider key.
export default defineConfig({
  ...recording,
  testMatch: 'live.spec.ts',
  outputDir: '../artifacts/live-e2e/playwright-recording',
  reporter: [
    ['list'],
    ['html', { outputFolder: '../artifacts/live-e2e/report', open: 'never' }],
    ['json', { outputFile: '../artifacts/live-e2e/results.json' }],
  ],
  use: { ...recording.use, headless: false },
});
