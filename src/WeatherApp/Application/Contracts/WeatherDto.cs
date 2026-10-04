namespace WeatherApp.Application.Contracts;

public sealed record WeatherDto(
    string City, string CountryCode, DateTimeOffset ObservedAtUtc,
    double WindSpeedMetersPerSecond, int WindDirectionDegrees,
    int VisibilityMeters, double PressureHpa, string SkyConditions,
    double TemperatureFahrenheit, double TemperatureCelsius,
    double? DewPointCelsius, bool DewPointIsCalculated, int RelativeHumidityPercent);
