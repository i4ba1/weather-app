import type { CityDto, CountryDto, CreatedNoteDto, CreateNoteCommand, ProblemDetails, WeatherDto } from './types';

async function request<T>(url: string, init: RequestInit): Promise<T> {
  let response: Response;
  try { response = await fetch(url, init); }
  catch (error) {
    if (init.signal?.aborted) throw error;
    throw new Error('Could not reach the weather server. Please try again.', { cause: error });
  }
  let payload: unknown;
  try { payload = await response.json(); }
  catch { throw new Error('The server returned an unreadable response. Please try again.'); }
  if (!response.ok) {
    const problem = payload as ProblemDetails | null;
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new Error(validation || problem?.detail || problem?.title || `Request failed (${response.status}).`);
  }
  return payload as T;
}

export const api = {
  countries: (signal: AbortSignal) => request<CountryDto[]>('/api/countries', { signal }),
  cities: (country: string, signal: AbortSignal) => request<CityDto[]>(`/api/countries/${encodeURIComponent(country)}/cities`, { signal }),
  weather: (city: string, country: string, signal: AbortSignal) => request<WeatherDto>(`/api/weather/${encodeURIComponent(city)}?countryCode=${encodeURIComponent(country)}`, { signal }),
  saveNote: (command: CreateNoteCommand, signal: AbortSignal) => request<CreatedNoteDto>('/api/weather/notes', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(command), signal,
  }),
};

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.';
}
