import { expect, test } from '@playwright/test';
import { weather } from '../src/test/fixtures';

test.beforeEach(async ({ page }) => {
  await page.route('http://127.0.0.1:5173/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const json = path === '/api/countries' ? [{ code: 'ID', name: 'Indonesia' }]
      : path.endsWith('/cities') ? [{ name: 'Jakarta', countryCode: 'ID' }]
      : path === '/api/weather/notes' ? { id: 'browser-note', createdAtUtc: '2023-11-14T22:13:20Z' }
      : weather;
    await route.fulfill({ status: path === '/api/weather/notes' ? 201 : 200, json });
  });
});

test('complete country city weather and note workflow', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Country', { exact: true }).selectOption('ID');
  await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
  await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toBeVisible();
  await expect(page.getByText('20.0 °C', { exact: true })).toBeVisible();
  await page.getByLabel('Weather note', { exact: true }).fill('Good day for a walk');
  await page.getByRole('button', { name: 'Save note' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Note saved' })).toContainText('browser-note');
});

test('weather failure explains the problem without showing metrics', async ({ page }) => {
  await page.route('**/api/weather/Jakarta?*', route => route.fulfill({ status: 503, json: { detail: 'Weather service is not configured.' } }));
  await page.goto('/');
  await page.getByLabel('Country', { exact: true }).selectOption('ID');
  await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
  await expect(page.getByRole('alert')).toContainText('Weather service is not configured.');
  await expect(page.getByText('20.0 °C', { exact: true })).toHaveCount(0);
});

test('note validation failures are shown to the user', async ({ page }) => {
  await page.route('**/api/weather/notes', route => route.fulfill({ status: 400, json: { title: 'Invalid request', errors: { Text: ['Note is invalid.'] } } }));
  await page.goto('/');
  await page.getByLabel('Country', { exact: true }).selectOption('ID');
  await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
  await page.getByLabel('Weather note', { exact: true }).fill('Test note');
  await page.getByRole('button', { name: 'Save note' }).click();
  await expect(page.getByRole('alert')).toContainText('Note is invalid.');
});

test('background multi-city refresh shows partial results and resumes after reload', async ({ page }) => {
  const jobId = '12345678-1234-1234-1234-123456789abc';
  const job = {
    id: jobId, createdAtUtc: '2023-11-14T22:13:20Z', status: 'CompletedWithErrors',
    cities: [
      { cityName: 'Jakarta', countryCode: 'ID', status: 'Completed', attempts: 1, weather, error: null },
      { cityName: 'Bandung', countryCode: 'ID', status: 'Failed', attempts: 3, weather: null, error: 'Synthetic timeout for browser test.' },
    ],
  };
  let submissions = 0;
  let polls = 0;
  await page.route('**/api/countries/ID/cities', route => route.fulfill({ json: [{ name: 'Jakarta', countryCode: 'ID' }, { name: 'Bandung', countryCode: 'ID' }] }));
  await page.route('**/api/weather/refresh-jobs', async route => {
    submissions++;
    expect(route.request().postDataJSON()).toEqual({ cities: [{ cityName: 'Jakarta', countryCode: 'ID' }, { cityName: 'Bandung', countryCode: 'ID' }] });
    await route.fulfill({ status: 202, json: { id: jobId } });
  });
  await page.route(`**/api/weather/refresh-jobs/${jobId}`, route => route.fulfill({ json: ++polls === 1 ? { ...job, status: 'Running' } : job }));
  await page.goto('/');
  await page.getByLabel('Country', { exact: true }).selectOption('ID');
  await page.getByRole('button', { name: 'Refresh all cities' }).click();
  await expect(page.getByText('Running', { exact: true })).toBeVisible();
  await expect(page.getByText('CompletedWithErrors', { exact: true })).toBeVisible();
  await expect(page.getByText('Synthetic timeout for browser test.')).toBeVisible();
  await expect(page.getByText('2 / 2 cities finished')).toBeVisible();
  await page.reload();
  await expect(page.getByText('CompletedWithErrors', { exact: true })).toBeVisible();
  expect(submissions).toBe(1);
});
