using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Contracts;
using WeatherApp.Application.Countries;
using WeatherApp.Application.Cities;
using WeatherApp.Application.Common;
using WeatherApp.Infrastructure.Locations;

namespace WeatherApp.Tests;

public class LocationQueryTests
{
    [Fact]
    public async Task QueriesReturnCatalogDataWithoutInfrastructureDependency()
    {
        var catalog = new FakeCatalog();
        var countries = await new GetCountriesHandler(catalog).Handle(new GetCountriesQuery(), default);
        var cities = await new GetCitiesHandler(catalog).Handle(new GetCitiesQuery("id"), default);
        Assert.Equal("Indonesia", Assert.Single(countries).Name);
        Assert.Equal("Jakarta", Assert.Single(cities).Name);
        await Assert.ThrowsAsync<LocationNotFoundException>(() =>
            new GetCitiesHandler(catalog).Handle(new GetCitiesQuery("XX"), default));
    }

    [Fact]
    public void BundledCatalogResolvesCaseInsensitiveSelections()
    {
        var catalog = new BundledLocationCatalog();
        Assert.True(catalog.Contains("id", "jakarta"));
        Assert.False(catalog.Contains("AU", "Jakarta"));
        Assert.Equal(3, catalog.GetCities("ID").Count);
        Assert.Equal(5, catalog.GetCountries().Count);
    }

    private sealed class FakeCatalog : ILocationCatalog
    {
        public IReadOnlyList<CountryDto> GetCountries() => [new("ID", "Indonesia")];
        public IReadOnlyList<CityDto> GetCities(string countryCode) =>
            countryCode.Equals("ID", StringComparison.OrdinalIgnoreCase) ? [new("Jakarta", "ID")] : [];
        public bool Contains(string countryCode, string cityName) => GetCities(countryCode).Any(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase));
    }
}
