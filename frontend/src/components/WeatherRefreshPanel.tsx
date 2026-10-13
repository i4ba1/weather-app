import { useEffect, useRef, useState } from 'react';
import { api, errorMessage } from '../api/client';
import type { CityDto, RefreshJobDto } from '../api/types';

const storageKey = 'weather-refresh-job';
function rememberedJob(): string {
  try { return localStorage.getItem(storageKey) ?? ''; } catch { return ''; }
}
function remember(id: string) {
  try { if (id) localStorage.setItem(storageKey, id); else localStorage.removeItem(storageKey); } catch { /* Tracking still works without browser storage. */ }
}
function finished(job: RefreshJobDto | null) {
  return job !== null && ['Completed', 'CompletedWithErrors', 'Failed'].includes(job.status);
}

export function WeatherRefreshPanel({ cities, loading }: { cities: CityDto[]; loading: boolean }) {
  const [jobId, setJobId] = useState(rememberedJob);
  const [job, setJob] = useState<RefreshJobDto | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const submission = useRef<AbortController | null>(null);
  useEffect(() => () => { submission.current?.abort(); }, []);

  useEffect(() => {
    if (!jobId) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    async function poll() {
      try {
        const result = await api.refreshJob(jobId, controller.signal);
        if (controller.signal.aborted) return;
        setJob(result); setError('');
        if (!finished(result)) timer = setTimeout(() => { void poll(); }, 1000);
      } catch (failure) {
        if (!controller.signal.aborted) setError(errorMessage(failure));
      }
    }
    void poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [jobId, revision]);

  async function refresh() {
    if (submission.current || !cities.length || (jobId && !finished(job))) return;
    const controller = new AbortController();
    submission.current = controller; setSubmitting(true); setError('');
    try {
      const accepted = await api.createRefreshJob({ cities: cities.map(city => ({ cityName: city.name, countryCode: city.countryCode })) }, controller.signal);
      if (!controller.signal.aborted) { remember(accepted.id); setJob(null); setJobId(accepted.id); }
    } catch (failure) {
      if (!controller.signal.aborted) setError(errorMessage(failure));
    } finally {
      submission.current = null;
      if (!controller.signal.aborted) setSubmitting(false);
    }
  }

  return <section className="card refresh-card" aria-labelledby="refresh-heading">
    <h2 id="refresh-heading">Refresh a country’s weather</h2>
    <p>Fetch all cities in the selected country. Accepted refreshes continue if you leave this page.</p>
    <button type="button" disabled={loading || !cities.length || submitting || Boolean(jobId && !finished(job))} onClick={() => { void refresh(); }}>
      {submitting ? 'Submitting refresh…' : 'Refresh all cities'}
    </button>
    {error && <div><p role="alert" className="error">{error}</p>
      {jobId && <button type="button" onClick={() => { setError(''); setRevision(value => value + 1); }}>Retry status</button>}</div>}
    {jobId && <div className="refresh-progress" aria-live="polite">
      <p>Job reference: <code>{jobId}</code></p>
      <p>Status: <strong>{job?.status ?? 'Checking status…'}</strong></p>
      {job && <><p>{job.cities.filter(city => city.status === 'Completed' || city.status === 'Failed').length} / {job.cities.length} cities finished</p>
        <ul>{job.cities.map(city => <li key={`${city.countryCode}:${city.cityName}`}>
          <strong>{city.cityName}, {city.countryCode}</strong> — {city.status} ({city.attempts} attempts)
          {city.weather && <p>{city.weather.city}: {city.weather.temperatureCelsius.toFixed(1)} °C · {city.weather.skyConditions}<br />
            Observed: {city.weather.observedAtUtc} · Refresh snapshot</p>}
          {city.error && <p className="error">{city.error}</p>}
        </li>)}</ul></>}
      <button type="button" onClick={() => { remember(''); setJobId(''); setJob(null); setError(''); }}>Dismiss tracking</button>
      <p className="muted">Dismissing tracking does not cancel server work.</p>
    </div>}
  </section>;
}
