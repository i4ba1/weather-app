import { expect, test } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import type { AcceptedRefreshJobDto, RefreshJobDto } from '../src/api/types';

test('headed real API workflow: note, parallel refresh, retry, partial failure and reload', async ({ page }, testInfo) => {
  // Every /api request goes to Kestrel. Only the server's provider HTTP transport is fake.
  await page.route('**/*', route => ['localhost', '127.0.0.1'].includes(new URL(route.request().url()).hostname)
    ? route.continue() : route.abort());
  await page.addInitScript(() => {
    document.addEventListener('DOMContentLoaded', () => {
      const banner = document.createElement('div');
      banner.textContent = 'SAMPLE WEATHER · Real React → .NET API → Channel workers → isolated SQLite';
      banner.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:10000;background:#a65f24;color:white;padding:10px;font:600 15px system-ui;text-align:center';
      document.body.prepend(banner); document.body.style.paddingTop = '38px';
    });
  });
  await page.goto('/');
  await page.getByLabel('Country', { exact: true }).selectOption('ID');
  const weatherResponse = page.waitForResponse(response => response.url().includes('/api/weather/Jakarta?'));
  await page.getByLabel('City', { exact: true }).selectOption('Jakarta');
  expect((await weatherResponse).status()).toBe(200);
  await expect(page.getByRole('heading', { name: 'Jakarta', exact: true })).toBeVisible();
  await expect(page.getByText('20.0 °C', { exact: true })).toBeVisible();
  await page.waitForTimeout(1400); // Presentation pause, assertions synchronize the test.

  await page.getByLabel('Weather note', { exact: true }).pressSequentially('Sample channel demo: warm in Jakarta.', { delay: 35 });
  const noteResponse = page.waitForResponse(response => response.url().endsWith('/api/weather/notes') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Save note' }).click();
  const note = await noteResponse;
  expect(note.status()).toBe(201);
  const savedNote = await note.json() as { id: string };
  await expect(page.getByRole('status').filter({ hasText: 'Note saved' })).toContainText(savedNote.id);
  await page.waitForTimeout(1000);

  await page.getByRole('heading', { name: 'Refresh a country’s weather' }).scrollIntoViewIfNeeded();
  const acceptedResponse = page.waitForResponse(response => response.url().endsWith('/api/weather/refresh-jobs') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Refresh all cities' }).click();
  const accepted = await acceptedResponse;
  expect(accepted.status()).toBe(202);
  const { id } = await accepted.json() as AcceptedRefreshJobDto;
  expect(accepted.headers().location).toBe(`/api/weather/refresh-jobs/${id}`);
  await expect(page.getByText('Running', { exact: true })).toBeVisible();
  await page.locator('#refresh-heading').evaluate(element => element.scrollIntoView({ block: 'start' }));
  await expect(page.getByText('CompletedWithErrors', { exact: true })).toBeVisible({ timeout: 25_000 });
  await expect(page.getByText('3 / 3 cities finished')).toBeVisible();
  const statusResponse = await page.request.get(`/api/weather/refresh-jobs/${id}`);
  expect(statusResponse.status()).toBe(200);
  const job = await statusResponse.json() as RefreshJobDto;
  expect(Object.fromEntries(job.cities.map(city => [city.cityName, [city.status, city.attempts]]))).toEqual({
    Jakarta: ['Completed', 1], Bandung: ['Completed', 2], Surabaya: ['Failed', 1],
  });
  expect(job.cities.find(city => city.cityName === 'Surabaya')!.weather).toBeNull();
  await page.locator('#refresh-heading').evaluate(element => element.scrollIntoView({ block: 'start' }));
  await expect(page.getByText('The weather provider could not supply valid weather data. Please try again.')).toBeInViewport();
  await page.waitForTimeout(2200);
  await page.screenshot({ path: testInfo.outputPath('refresh-completed.png'), fullPage: true });

  await page.reload();
  await page.getByRole('heading', { name: 'Refresh a country’s weather' }).scrollIntoViewIfNeeded();
  await expect(page.getByText('CompletedWithErrors', { exact: true })).toBeVisible();
  await expect(page.getByText(id, { exact: true })).toBeVisible();
  await page.locator('#refresh-heading').evaluate(element => element.scrollIntoView({ block: 'start' }));
  await page.waitForTimeout(1800);
  const folder = resolve(process.cwd(), '../artifacts/channels-e2e');
  await mkdir(folder, { recursive: true });
  await writeFile(resolve(folder, 'verified-api-results.json'), JSON.stringify({ noteId: savedNote.id, job }, null, 2));
  await testInfo.attach('real-api-results', { body: Buffer.from(JSON.stringify(job, null, 2)), contentType: 'application/json' });
});
