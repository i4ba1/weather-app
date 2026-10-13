import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';
import App from './App';
import { weather } from './test/fixtures';

const reply = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const job = {
  id: 'job-123', createdAtUtc: '2023-11-14T22:13:20Z', status: 'CompletedWithErrors',
  cities: [
    { cityName: 'Jakarta', countryCode: 'ID', status: 'Completed', attempts: 1, weather, error: null },
    { cityName: 'Bandung', countryCode: 'ID', status: 'Failed', attempts: 3, weather: null, error: 'Provider did not respond.' },
  ],
};
const send = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();
beforeEach(() => {
  localStorage.clear();
  send.mockReset();
  send.mockImplementation(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url === '/api/countries') return reply([{ code: 'ID', name: 'Indonesia' }]);
    if (url.endsWith('/cities')) return reply([{ name: 'Jakarta', countryCode: 'ID' }, { name: 'Bandung', countryCode: 'ID' }]);
    if (url === '/api/weather/refresh-jobs') return reply({ id: 'job-123' }, 202);
    if (url.endsWith('/job-123')) return reply(job);
    throw new Error(`Unexpected request ${url}`);
  });
  vi.stubGlobal('fetch', send);
});

test('refresh submits country cities and renders real results alongside partial failures', async () => {
  render(<App />);
  const user = userEvent.setup();
  await screen.findByRole('option', { name: 'Indonesia' });
  await user.selectOptions(screen.getByLabelText('Country'), 'ID');
  await screen.findByRole('option', { name: 'Jakarta' });
  await user.click(screen.getByRole('button', { name: 'Refresh all cities' }));
  expect(await screen.findByText('CompletedWithErrors')).toBeInTheDocument();
  expect(screen.getByText('Provider did not respond.')).toBeInTheDocument();
  expect(screen.getByText(/Jakarta.*20.0/)).toBeInTheDocument();
  expect(localStorage.getItem('weather-refresh-job')).toBe('job-123');
  const call = send.mock.calls.find(([url]) => url === '/api/weather/refresh-jobs');
  expect(JSON.parse(call![1]!.body as string)).toEqual({ cities: [{ cityName: 'Jakarta', countryCode: 'ID' }, { cityName: 'Bandung', countryCode: 'ID' }] });
});

test('browser reload restores job and status errors offer retry without another submission', async () => {
  localStorage.setItem('weather-refresh-job', 'job-123');
  const original = send.getMockImplementation()!;
  let fail = true;
  send.mockImplementation((input: RequestInfo | URL) => String(input).endsWith('/job-123') && fail
    ? Promise.resolve(reply({ detail: 'Status temporarily unavailable.' }, 503)) : original(input));
  render(<App />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Status temporarily unavailable.');
  fail = false;
  await userEvent.click(screen.getByRole('button', { name: 'Retry status' }));
  expect(await screen.findByText('CompletedWithErrors')).toBeInTheDocument();
  expect(send.mock.calls.some(([url]) => url === '/api/weather/refresh-jobs')).toBe(false);
});

test('pending job polls until terminal and stops polling when unmounted', async () => {
  localStorage.setItem('weather-refresh-job', 'job-123');
  const original = send.getMockImplementation()!;
  let polls = 0;
  let signal: AbortSignal | undefined;
  send.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input).endsWith('/job-123')) {
      signal = init?.signal as AbortSignal;
      return Promise.resolve(reply(++polls === 1 ? { ...job, status: 'Running' } : job));
    }
    return original(input);
  });
  const view = render(<App />);
  expect(await screen.findByText('Running')).toBeInTheDocument();
  await waitFor(() => expect(screen.getByText('CompletedWithErrors')).toBeInTheDocument(), { timeout: 4000 });
  view.unmount();
  expect(signal!.aborted).toBe(true);
  expect(polls).toBe(2);
});
