import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './recording', testMatch: 'channels.spec.ts',
  outputDir: '../artifacts/channels-e2e/playwright-recording',
  workers: 1, timeout: 90_000,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../artifacts/channels-e2e/report', open: 'never' }],
    ['json', { outputFile: '../artifacts/channels-e2e/results.json' }],
  ],
  use: {
    baseURL: 'http://127.0.0.1:5175', headless: false,
    viewport: { width: 1440, height: 1120 },
    video: { mode: 'on', size: { width: 1440, height: 1120 } },
    trace: 'on', launchOptions: { slowMo: 180 },
  },
  webServer: [
    {
      command: 'dotnet run --project ../tests/WeatherApp.RecordingHost -c Release',
      url: 'http://127.0.0.1:5105/api/countries', reuseExistingServer: false, timeout: 120_000,
    },
    {
      command: 'npm run dev -- --port 5175 --strictPort',
      url: 'http://127.0.0.1:5175', reuseExistingServer: false,
      env: { WEATHER_API_PROXY: 'http://127.0.0.1:5105' },
    },
  ],
  projects: [{ name: 'headed-chromium', use: { browserName: 'chromium' } }],
});
