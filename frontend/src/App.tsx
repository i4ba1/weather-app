import { useEffect, useState } from 'react';
import { api, errorMessage } from './api/client';
import type { CityDto, CountryDto, WeatherDto } from './api/types';
import { LocationSelector } from './components/LocationSelector';
import { WeatherDetails } from './components/WeatherDetails';
import { NoteForm } from './components/NoteForm';

export default function App() {
  const [countries, setCountries] = useState<CountryDto[]>([]);
  const [cities, setCities] = useState<CityDto[]>([]);
  const [country, setCountry] = useState('');
  const [city, setCity] = useState('');
  const [weather, setWeather] = useState<WeatherDto | null>(null);
  const [countriesLoading, setCountriesLoading] = useState(true);
  const [citiesLoading, setCitiesLoading] = useState(false);
  const [weatherLoading, setWeatherLoading] = useState(false);
  const [locationError, setLocationError] = useState('');
  const [weatherError, setWeatherError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    api.countries(controller.signal).then(data => { if (!controller.signal.aborted) setCountries(data); })
      .catch(error => { if (!controller.signal.aborted) setLocationError(errorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setCountriesLoading(false); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!country) return;
    const controller = new AbortController();
    api.cities(country, controller.signal).then(data => { if (!controller.signal.aborted) setCities(data); })
      .catch(error => { if (!controller.signal.aborted) setLocationError(errorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setCitiesLoading(false); });
    return () => controller.abort();
  }, [country]);

  useEffect(() => {
    if (!city || !country) return;
    const controller = new AbortController();
    api.weather(city, country, controller.signal).then(data => { if (!controller.signal.aborted) setWeather(data); })
      .catch(error => { if (!controller.signal.aborted) setWeatherError(errorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setWeatherLoading(false); });
    return () => controller.abort();
  }, [city, country]);

  function changeCountry(value: string) {
    setCountry(value); setCity(''); setCities([]); setWeather(null);
    setLocationError(''); setWeatherError(''); setCitiesLoading(Boolean(value)); setWeatherLoading(false);
  }
  function changeCity(value: string) {
    setCity(value); setWeather(null); setWeatherError(''); setWeatherLoading(Boolean(value));
  }

  return <div className="app-shell">
    <header className="site-header"><a href="/" className="brand"><span aria-hidden="true">◒</span> Xtramile<span className="brand-light"> / weather</span></a>
      <span className="header-note">A window to the weather</span></header>
    <main>
      <div className="intro"><p className="eyebrow">Every city has a forecast</p><h1>What’s the weather<br />like over there?</h1>
        <p>Explore current conditions. Keep a little observation.</p></div>
      <div className="dashboard"><div className="controls">
        <LocationSelector countries={countries} cities={cities} country={country} city={city}
          countriesLoading={countriesLoading} citiesLoading={citiesLoading} onCountryChange={changeCountry} onCityChange={changeCity} />
        {locationError && <p role="alert" className="error">{locationError}</p>}
        <NoteForm key={`${country}:${city}`} city={city} country={country} />
      </div><div className="conditions" aria-live="polite" aria-busy={weatherLoading}>
        {weatherLoading ? <section className="card empty-state"><p role="status">Getting current weather…</p></section>
          : weather ? <WeatherDetails weather={weather} />
          : <section className="card empty-state"><span className="empty-symbol" aria-hidden="true">☁</span>
            <h2>{weatherError ? 'Weather is unavailable' : 'A new perspective awaits'}</h2>
            <p>{weatherError ? 'Try another city or come back in a moment.' : 'Choose a destination to see its current conditions.'}</p>
            {weatherError && <p role="alert" className="error">{weatherError}</p>}</section>}
      </div></div>
    </main>
    <footer>Weather observations from OpenWeatherMap · Times shown in UTC</footer>
  </div>;
}
