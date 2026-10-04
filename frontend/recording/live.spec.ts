import { expect, test } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import type { WeatherDto } from '../src/api/types';

test('headed live frontend to backend weather and note workflow', async ({ page }, testInfo) => {
  const artifactFolder = resolve(process.cwd(), '../artifacts/live-e2e');
  await mkdir(artifactFolder, { recursive: true });
  const calls: { method: string; path: string; status: number }[] = [];
  page.on('response', response => {
    const url = new URL(response.url());
    if (url.pathname.startsWith('/api/'))
      calls.push({ method: response.request().method(), path: url.pathname, status: response.status() });
  });
  // No interception: every request uses Vite, ASP.NET Core, and the live provider.
  await page.goto('/');
  await expect(page.getByLabel('Country', { exact: true })).toBeVisible();
  await page.evaluate(() => {
    const banner = document.createElement('div');
    banner.id = 'live-caption';
    banner.textContent = 'LIVE E2E - React frontend → ASP.NET Core → OpenWeatherMap / SQLite';
    banner.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:10000;background:#27614b;color:#fff;padding:10px 20px;font:600 15px system-ui;text-align:center';
    document.body.prepend(banner);
    document.body.style.paddingTop = '38px';
  });
  await test.step('Read actual country, city and live weather responses', async () => {
    await page.getByLabel('Country', { exact: true }).selectOption('ID');
    await expect(page.getByRole('option', { name: 'Jakarta', exact: true })).toBeAttached();
    const weatherResponse = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/weather/Jakarta');
    await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
    const response = await weatherResponse;
    expect(response.status()).toBe(200);
    const weather = await response.json() as WeatherDto;
    expect(weather.city).toBe('Jakarta');
    expect(weather.countryCode).toBe('ID');
    expect(Number.isFinite(weather.temperatureCelsius)).toBeTruthy();
    await expect(page.getByText(`${weather.temperatureCelsius.toFixed(1)} °C`, { exact: true })).toBeVisible();
    await expect(page.getByText(weather.skyConditions, { exact: true })).toBeVisible();
    await writeFile(resolve(artifactFolder, 'live-weather.json'), JSON.stringify(weather, null, 2));
    await page.waitForTimeout(2200);
  });
  await test.step('Create a real note and verify backend acknowledgement', async () => {
    const note = { cityName: 'Jakarta', countryCode: 'ID', text: `Live headed E2E: Jakarta weather checked at ${new Date().toISOString()}.` };
    await page.getByLabel('Weather note', { exact: true }).pressSequentially(note.text, { delay: 55 });
    const savedResponse = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/weather/notes' && response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Save note' }).click();
    const response = await savedResponse;
    expect(response.status()).toBe(201);
    expect(response.request().postDataJSON()).toEqual(note);
    const saved = await response.json() as { id: string; createdAtUtc: string };
    expect(saved.id).toMatch(/^[0-9a-f-]{36}$/i);
    await expect(page.getByRole('status').filter({ hasText: 'Note saved' })).toContainText(saved.id);
    await writeFile(resolve(artifactFolder, 'created-note.json'), JSON.stringify({ ...note, ...saved }, null, 2));
    await page.waitForTimeout(2000);
    await page.screenshot({ path: testInfo.outputPath('live-note-saved.png'), fullPage: true });
  });
  await test.step('Reset the selected city when changing country', async () => {
    await page.getByLabel('Country', { exact: true }).selectOption('AU');
    await expect(page.getByLabel('City', { exact: true })).toHaveValue('');
    await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toHaveCount(0);
    await expect(page.getByRole('option', { name: 'Sydney', exact: true })).toBeAttached();
    await expect(page.getByRole('button', { name: 'Save note' })).toBeDisabled();
    await page.locator('#live-caption').evaluate(element => {
      element.textContent = 'PASS - Real country/city API, live weather, SQLite note save, country reset';
    });
    await page.waitForTimeout(2200);
  });
  expect(calls).toEqual(expect.arrayContaining([
    { method: 'GET', path: '/api/countries', status: 200 },
    { method: 'GET', path: '/api/countries/ID/cities', status: 200 },
    { method: 'GET', path: '/api/weather/Jakarta', status: 200 },
    { method: 'POST', path: '/api/weather/notes', status: 201 },
  ]));
  await writeFile(resolve(artifactFolder, 'api-calls.json'), JSON.stringify(calls, null, 2));
});
