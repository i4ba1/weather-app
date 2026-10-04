import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';
import App from './App';
import { weather } from './test/fixtures';

const reply = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
const send = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

beforeEach(() => {
  send.mockReset();
  send.mockImplementation(async (input) => {
    const url = String(input);
    if (url === '/api/countries') return reply([{ code: 'ID', name: 'Indonesia' }, { code: 'AU', name: 'Australia' }]);
    if (url === '/api/countries/ID/cities') return reply([{ name: 'Jakarta', countryCode: 'ID' }, { name: 'Bandung', countryCode: 'ID' }]);
    if (url === '/api/countries/AU/cities') return reply([{ name: 'Sydney', countryCode: 'AU' }]);
    if (url.startsWith('/api/weather/Jakarta')) return reply(weather);
    if (url.startsWith('/api/weather/Bandung')) return reply({ ...weather, city: 'Bandung' });
    if (url === '/api/weather/notes') return reply({ id: 'note-123', createdAtUtc: '2023-11-14T22:13:20Z' }, 201);
    throw new Error(`Unhandled test request: ${url}`);
  });
  vi.stubGlobal('fetch', send);
});

async function selectJakarta() {
  const user = userEvent.setup();
  await screen.findByRole('option', { name: 'Indonesia' });
  await user.selectOptions(screen.getByLabelText('Country'), 'ID');
  await screen.findByRole('option', { name: 'Jakarta' });
  await user.selectOptions(screen.getByLabelText('City'), 'Jakarta');
  return user;
}

test('selection renders every weather metric and saves the selected city note', async () => {
  render(<App />);
  expect(screen.getByRole('button', { name: 'Save note' })).toBeDisabled();
  const user = await selectJakarta();
  await screen.findByRole('heading', { name: 'Jakarta' });
  for (const text of ['20.0 °C', '68.0 °F', 'Clear', '50%', '1,013 hPa', '10,000 m', '4.5 m/s', '180°', '9.3 °C', '2023-11-14 22:13:20 UTC'])
    expect(screen.getByText(text)).toBeInTheDocument();
  await user.type(screen.getByLabelText('Weather note'), 'Sunny today');
  await user.click(screen.getByRole('button', { name: 'Save note' }));
  expect(await screen.findByText(/Note saved.*note-123/)).toBeInTheDocument();
  const call = send.mock.calls.find(([url]) => url === '/api/weather/notes');
  expect(JSON.parse(call![1]!.body as string)).toEqual({ cityName: 'Jakarta', countryCode: 'ID', text: 'Sunny today' });
});

test('changing country clears the old weather and city', async () => {
  render(<App />);
  const user = await selectJakarta();
  await screen.findByRole('heading', { name: 'Jakarta' });
  await user.selectOptions(screen.getByLabelText('Country'), 'AU');
  expect(screen.queryByRole('heading', { name: 'Jakarta' })).not.toBeInTheDocument();
  expect(screen.getByLabelText('City')).toHaveValue('');
  expect(screen.getByRole('button', { name: 'Save note' })).toBeDisabled();
  await screen.findByRole('option', { name: 'Sydney' });
});

test('weather provider errors are visible without fabricated metrics', async () => {
  const original = send.getMockImplementation()!;
  send.mockImplementation((input, init) => String(input).startsWith('/api/weather/Jakarta')
    ? Promise.resolve(reply({ detail: 'Weather service is not configured.' }, 503)) : original(input, init));
  render(<App />);
  await selectJakarta();
  expect(await screen.findByRole('alert')).toHaveTextContent('Weather service is not configured.');
  expect(screen.queryByText('20.0 °C')).not.toBeInTheDocument();
});

test('a late weather response cannot replace the current city', async () => {
  let finish!: (response: Response) => void;
  const original = send.getMockImplementation()!;
  send.mockImplementation((input, init) => String(input).startsWith('/api/weather/Jakarta')
    ? new Promise(resolve => { finish = resolve; }) : original(input, init));
  render(<App />);
  const user = await selectJakarta();
  await user.selectOptions(screen.getByLabelText('City'), 'Bandung');
  await screen.findByRole('heading', { name: 'Bandung' });
  finish(reply(weather));
  await waitFor(() => expect(screen.queryByRole('heading', { name: 'Jakarta' })).not.toBeInTheDocument());
  expect(screen.getByRole('heading', { name: 'Bandung' })).toBeInTheDocument();
});

test('pending note saves cannot be duplicated or show feedback for another city', async () => {
  let finish!: (response: Response) => void;
  const original = send.getMockImplementation()!;
  send.mockImplementation((input, init) => String(input) === '/api/weather/notes'
    ? new Promise(resolve => { finish = resolve; }) : original(input, init));
  render(<App />);
  const user = await selectJakarta();
  await user.type(screen.getByLabelText('Weather note'), 'Sunny');
  await user.click(screen.getByRole('button', { name: 'Save note' }));
  expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled();
  await user.selectOptions(screen.getByLabelText('City'), 'Bandung');
  finish(reply({ id: 'old-note', createdAtUtc: '2023-11-14T22:13:20Z' }, 201));
  await screen.findByRole('heading', { name: 'Bandung' });
  expect(screen.queryByText(/old-note/)).not.toBeInTheDocument();
  expect(send.mock.calls.filter(([url]) => url === '/api/weather/notes')).toHaveLength(1);
});

test('country loading failures explain why selection is unavailable', async () => {
  send.mockResolvedValue(reply({ title: 'Server error' }, 500));
  render(<App />);
  expect(await screen.findByRole('alert')).toHaveTextContent('Server error');
  expect(screen.getByLabelText('Country')).toBeDisabled();
});
