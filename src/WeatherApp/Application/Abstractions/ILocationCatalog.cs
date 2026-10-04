using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.Abstractions;

public interface ILocationCatalog
{
    IReadOnlyList<CountryDto> GetCountries();
    IReadOnlyList<CityDto> GetCities(string countryCode);
    bool Contains(string countryCode, string cityName);
}
