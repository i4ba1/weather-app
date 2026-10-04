import { expect, test } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import sample from './sample-data.json' with { type: 'json' };

test('record sample weather selection and a real backend note save', async ({ page, request }, testInfo) => {
  const countriesResponse = await request.get('http://localhost:5000/api/countries');
  expect(countriesResponse.ok()).toBeTruthy();
  expect(await countriesResponse.json()).toContainEqual({ code: 'ID', name: 'Indonesia' });

  // Only weather is synthetic. Catalog requests and POST notes go through Vite to the actual backend.
  await page.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['127.0.0.1', 'localhost'].includes(url.hostname) ? route.continue() : route.abort();
  });
  await page.route('http://127.0.0.1:5173/api/weather/Jakarta?*', route => route.fulfill({ json: sample.weather }));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /What’s the weather/ })).toBeVisible();
  await page.evaluate(() => {
    const banner = document.createElement('div');
    banner.id = 'e2e-caption';
    banner.textContent = 'SAMPLE E2E RECORDING · Synthetic weather / real backend and SQLite';
    banner.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:10000;background:#b06a29;color:#fff;padding:10px 20px;font:600 15px system-ui;text-align:center';
    document.body.prepend(banner);
    document.body.style.paddingTop = '38px';
  });

  await test.step('Load real country and city data', async () => {
    await page.getByLabel('Country', { exact: true }).selectOption('ID');
    await expect(page.getByRole('option', { name: 'Jakarta', exact: true })).toBeAttached();
    await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
  });

  await test.step('Display the sample weather metrics', async () => {
    await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toBeVisible();
    for (const value of ['20.0 °C', '68.0 °F', 'Clear', '50%', '1,013 hPa', '10,000 m', '4.5 m/s', '180°', '9.3 °C'])
      await expect(page.getByText(value, { exact: true })).toBeVisible();
    // Short presentation pauses make the recording readable; assertions handle synchronization.
    await page.waitForTimeout(1600);
  });

  await test.step('Save the sample note through the real API', async () => {
    await page.locator('#e2e-caption').evaluate(element => {
      element.textContent = 'SAMPLE E2E RECORDING · Saving a note through the real API to SQLite';
    });
    await page.getByLabel('Weather note', { exact: true }).pressSequentially(sample.note.text, { delay: 55 });
    const responsePromise = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/weather/notes' && response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Save note' }).click();
    const response = await responsePromise;
    expect(response.status()).toBe(201);
    expect(response.request().postDataJSON()).toEqual(sample.note);
    const saved = await response.json() as { id: string; createdAtUtc: string };
    expect(saved.id).toMatch(/^[0-9a-f-]{36}$/i);
    await expect(page.getByRole('status').filter({ hasText: 'Note saved' })).toContainText(saved.id);
    const artifactFolder = resolve(process.cwd(), '../artifacts/e2e');
    await mkdir(artifactFolder, { recursive: true });
    await writeFile(resolve(artifactFolder, 'created-note.json'), JSON.stringify({ ...sample.note, ...saved }, null, 2));
    await testInfo.attach('sample-data', { body: Buffer.from(JSON.stringify(sample, null, 2)), contentType: 'application/json' });
    await testInfo.attach('created-note', { body: Buffer.from(JSON.stringify(saved)), contentType: 'application/json' });
    await page.waitForTimeout(1600);
    await page.screenshot({ path: testInfo.outputPath('note-saved.png'), fullPage: true });
  });

  await test.step('Switching country clears the previous weather and city', async () => {
    await page.locator('#e2e-caption').evaluate(element => {
      element.textContent = 'SAMPLE E2E RECORDING · Changing country clears the previous selection';
    });
    await page.getByLabel('Country', { exact: true }).selectOption('AU');
    await expect(page.getByLabel('City', { exact: true })).toHaveValue('');
    await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toHaveCount(0);
    await expect(page.getByRole('option', { name: 'Sydney', exact: true })).toBeAttached();
    await expect(page.getByRole('button', { name: 'Save note' })).toBeDisabled();
    await page.waitForTimeout(1000);
    await page.getByLabel('Country', { exact: true }).selectOption('ID');
    await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
    await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toBeVisible();
    await page.locator('#e2e-caption').evaluate(element => {
      element.textContent = 'PASS · Country/city selection, sample weather, real note save and selection reset';
    });
    await page.waitForTimeout(2000);
  });
});
