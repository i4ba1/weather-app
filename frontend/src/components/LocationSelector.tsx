import type { CityDto, CountryDto } from '../api/types';

interface Props {
  countries: CountryDto[]; cities: CityDto[]; country: string; city: string;
  countriesLoading: boolean; citiesLoading: boolean;
  onCountryChange: (value: string) => void; onCityChange: (value: string) => void;
}

export function LocationSelector(props: Props) {
  return <section className="card location-card" aria-labelledby="location-heading">
    <div className="section-number" aria-hidden="true">01</div>
    <h2 id="location-heading">Choose your destination</h2>
    <p className="muted">Start with a country, then find your city.</p>
    <div className="selectors">
      <div><label htmlFor="country">Country</label>
        <select id="country" value={props.country} disabled={props.countriesLoading || !props.countries.length}
          onChange={event => props.onCountryChange(event.target.value)}>
          <option value="">{props.countriesLoading ? 'Loading countries…' : 'Select a country'}</option>
          {props.countries.map(country => <option key={country.code} value={country.code}>{country.name}</option>)}
        </select>
      </div>
      <div><label htmlFor="city">City</label>
        <select id="city" value={props.city} disabled={!props.country || props.citiesLoading || !props.cities.length}
          onChange={event => props.onCityChange(event.target.value)}>
          <option value="">{props.citiesLoading ? 'Loading cities…' : 'Select a city'}</option>
          {props.cities.map(city => <option key={city.name} value={city.name}>{city.name}</option>)}
        </select>
      </div>
    </div>
    <p className="catalog-note">Available locations: Australia, Indonesia, Japan, Singapore and the United Kingdom.</p>
  </section>;
}
