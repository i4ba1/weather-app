using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Application.Weather;
using WeatherApp.Infrastructure.Locations;

namespace WeatherApp.Tests;

public class WeatherQueryTests
{
    [Fact]
    public async Task ResolvesCanonicalLocationAndPreservesMetrics()
    {
        var service = new FakeWeatherService();
        var result = await new GetWeatherHandler(new BundledLocationCatalog(), service)
            .Handle(new GetWeatherQuery("jakarta", "id"), default);
        Assert.Equal("Jakarta", result.City);
        Assert.Equal("ID", result.CountryCode);
        Assert.Equal(20, result.TemperatureCelsius);
        Assert.Equal(68, result.TemperatureFahrenheit);
    }

    [Fact]
    public async Task RejectsUnknownLocationBeforeProviderCall() =>
        await Assert.ThrowsAsync<LocationNotFoundException>(() =>
            new GetWeatherHandler(new BundledLocationCatalog(), new FakeWeatherService())
                .Handle(new GetWeatherQuery("Unknown", "ID"), default));

    [Fact]
    public async Task PreservesCancellation() =>
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetWeatherHandler(new BundledLocationCatalog(), new FakeWeatherService())
                .Handle(new GetWeatherQuery("Jakarta", "ID"), new CancellationToken(true)));
}

internal sealed class FakeWeatherService : IWeatherService
{
    public Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cityName != "Jakarta" || countryCode != "ID") throw new InvalidOperationException("Selection was not canonicalized.");
        return Task.FromResult(new WeatherDto(cityName, countryCode, DateTimeOffset.UnixEpoch,
            4.4704, 180, 10000, 1013, "Clear", 68, 20, 9.26, true, 50));
    }
}
