namespace WeatherApp.Application.Common;

public enum WeatherFailure { Configuration, Upstream, Timeout }

public sealed class WeatherServiceException(WeatherFailure failure)
    : Exception(failure switch
    {
        WeatherFailure.Configuration => "Weather service is not configured. Set the server's OpenWeatherMap API key.",
        WeatherFailure.Timeout => "The weather provider did not respond in time. Please try again.",
        _ => "The weather provider could not supply valid weather data. Please try again."
    })
{
    public WeatherFailure Failure { get; } = failure;
}
