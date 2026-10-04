import type { WeatherDto } from '../api/types';

export function WeatherDetails({ weather }: { weather: WeatherDto }) {
  const metrics = [
    ['Wind speed', `${weather.windSpeedMetersPerSecond.toFixed(1)} m/s`],
    ['Wind direction', `${weather.windDirectionDegrees}°`],
    ['Visibility', `${weather.visibilityMeters.toLocaleString('en-US')} m`],
    ['Pressure', `${weather.pressureHpa.toLocaleString('en-US')} hPa`],
    ['Relative humidity', `${weather.relativeHumidityPercent}%`],
    [weather.dewPointIsCalculated ? 'Dew point · calculated' : 'Dew point', weather.dewPointCelsius === null ? 'Unavailable' : `${weather.dewPointCelsius.toFixed(1)} °C`],
  ];
  return <section className="card weather-card" aria-labelledby="weather-heading">
    <div className="weather-top"><div><p className="eyebrow">Current conditions · {weather.countryCode}</p>
      <h2 id="weather-heading">{weather.city}</h2><p className="sky">{weather.skyConditions}</p></div>
      <span className="weather-symbol" aria-hidden="true">☀</span></div>
    <div className="temperature"><strong>{weather.temperatureCelsius.toFixed(1)} °C</strong><span>{weather.temperatureFahrenheit.toFixed(1)} °F</span></div>
    <dl className="metrics">{metrics.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
    <p className="observation">Observed <time dateTime={weather.observedAtUtc}>{new Date(weather.observedAtUtc).toISOString().slice(0, 19).replace('T', ' ')} UTC</time></p>
  </section>;
}
