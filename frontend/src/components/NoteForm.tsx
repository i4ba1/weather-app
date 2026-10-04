import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, errorMessage } from '../api/client';

export function NoteForm({ city, country }: { city: string; country: string }) {
  const [text, setText] = useState('');
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [error, setError] = useState('');
  const activeRequest = useRef<AbortController | null>(null);
  const pending = useRef(false);
  useEffect(() => () => { activeRequest.current?.abort(); }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!city || !country || pending.current) return;
    setFeedback('');
    setError('');
    if (!text.trim()) { setError('Please enter a weather note.'); return; }
    const controller = new AbortController();
    activeRequest.current = controller;
    pending.current = true;
    setSaving(true);
    try {
      const result = await api.saveNote({ cityName: city, countryCode: country, text: text.trim() }, controller.signal);
      if (!controller.signal.aborted) { setFeedback(`Note saved. Reference: ${result.id}`); setText(''); }
    } catch (error) {
      if (!controller.signal.aborted) setError(errorMessage(error));
    } finally {
      pending.current = false;
      if (!controller.signal.aborted) setSaving(false);
    }
  }

  return <section className="card note-card" aria-labelledby="note-heading">
    <div className="section-number" aria-hidden="true">02</div>
    <h2 id="note-heading">Leave a weather note</h2>
    <p className="muted">{city ? `Your observation for ${city}.` : 'Select a city to save your observation.'}</p>
    <form onSubmit={submit}>
      <label htmlFor="note">Weather note</label>
      <textarea id="note" value={text} onChange={event => setText(event.target.value)} maxLength={2000}
        disabled={!city || saving} rows={4} placeholder="A little breezy, perfect for an afternoon walk…" />
      <div className="form-footer"><span className="muted">{text.length} / 2,000</span>
        <button type="submit" disabled={!city || !country || saving}>{saving ? 'Saving…' : 'Save note'}</button></div>
    </form>
    {error && <p role="alert" className="error">{error}</p>}
    {feedback && <p role="status" className="success">{feedback}</p>}
  </section>;
}
