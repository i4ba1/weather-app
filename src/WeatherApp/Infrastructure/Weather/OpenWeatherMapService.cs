using System.Text.Json;
using Microsoft.Extensions.Options;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.Weather;

public sealed class OpenWeatherMapService(HttpClient client, IOptions<OpenWeatherMapOptions> options) : IWeatherService
{
    public async Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new WeatherServiceException(WeatherFailure.Configuration);
        var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cityName + "," + countryCode)}&units=imperial&appid={Uri.EscapeDataString(key)}";
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new WeatherServiceException(WeatherFailure.Upstream,
                response.StatusCode is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.RequestTimeout
                    || (int)response.StatusCode >= 500);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return Map(json.RootElement, cityName, countryCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherServiceException(WeatherFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            // Do not attach the exception: it can include the request URI and API key.
            throw new WeatherServiceException(WeatherFailure.Upstream, isTransient: true);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException or OverflowException)
        {
            throw new WeatherServiceException(WeatherFailure.Upstream);
        }
    }

    private static WeatherDto Map(JsonElement root, string cityName, string countryCode)
    {
        var main = root.GetProperty("main");
        var wind = root.GetProperty("wind");
        var fahrenheit = Number(main.GetProperty("temp"), -200, 200);
        var celsius = Temperature.ToCelsius(fahrenheit);
        var humidity = main.GetProperty("humidity").GetInt32();
        if (humidity is < 0 or > 100) throw new JsonException();
        var pressure = Number(main.GetProperty("pressure"), 1, 1200);
        var speed = Number(wind.GetProperty("speed"), 0, 500) * 0.44704;
        var direction = wind.TryGetProperty("deg", out var degrees) ? degrees.GetInt32() : speed == 0 ? 0 : throw new JsonException();
        var visibility = root.GetProperty("visibility").GetInt32();
        if (direction is < 0 or > 360 || visibility < 0) throw new JsonException();
        var conditions = root.GetProperty("weather");
        if (conditions.ValueKind != JsonValueKind.Array || conditions.GetArrayLength() == 0) throw new JsonException();
        var sky = conditions[0].GetProperty("main").GetString();
        var country = root.GetProperty("sys").GetProperty("country").GetString();
        if (string.IsNullOrWhiteSpace(sky) || !countryCode.Equals(country, StringComparison.OrdinalIgnoreCase)) throw new JsonException();
        var observed = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("dt").GetInt64());
        double? dewPoint = null;
        if (humidity > 0)
        {
            var gamma = Math.Log(humidity / 100d) + 17.62 * celsius / (243.12 + celsius);
            dewPoint = 243.12 * gamma / (17.62 - gamma);
            if (!double.IsFinite(dewPoint.Value)) throw new JsonException();
        }
        return new WeatherDto(cityName, countryCode, observed, speed, direction, visibility, pressure,
            sky, fahrenheit, celsius, dewPoint, true, humidity);
    }

    private static double Number(JsonElement element, double min, double max)
    {
        var value = element.GetDouble();
        if (!double.IsFinite(value) || value < min || value > max) throw new JsonException();
        return value;
    }
}
