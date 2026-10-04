using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.Abstractions;

public interface IWeatherService
{
    Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken);
}
