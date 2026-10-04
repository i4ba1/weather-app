using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Contracts;

namespace WeatherApp.Infrastructure.Locations;

public sealed class BundledLocationCatalog : ILocationCatalog
{
    private static readonly IReadOnlyList<CountryDto> Countries = Array.AsReadOnly<CountryDto>(
        [new("AU", "Australia"), new("ID", "Indonesia"), new("JP", "Japan"), new("SG", "Singapore"), new("GB", "United Kingdom")]);
    private static readonly Dictionary<string, IReadOnlyList<CityDto>> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AU"] = Make("AU", "Sydney", "Melbourne", "Brisbane"),
        ["ID"] = Make("ID", "Jakarta", "Bandung", "Surabaya"),
        ["JP"] = Make("JP", "Tokyo", "Osaka", "Kyoto"),
        ["SG"] = Make("SG", "Singapore"),
        ["GB"] = Make("GB", "London", "Manchester", "Birmingham")
    };

    private static IReadOnlyList<CityDto> Make(string code, params string[] names) =>
        Array.AsReadOnly(names.Order().Select(name => new CityDto(name, code)).ToArray());
    public IReadOnlyList<CountryDto> GetCountries() => Countries;
    public IReadOnlyList<CityDto> GetCities(string countryCode) => Cities.GetValueOrDefault(countryCode, []);
    public bool Contains(string countryCode, string cityName) => GetCities(countryCode)
        .Any(city => city.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase));
}
