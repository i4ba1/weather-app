using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.RefreshJobs;

public sealed record RefreshLocationDto(string CityName, string CountryCode);
public sealed record AcceptedRefreshJobDto(Guid Id);
public sealed record RefreshCityDto(string CityName, string CountryCode, string Status, int Attempts, WeatherDto? Weather, string? Error);
public sealed record RefreshJobDto(Guid Id, DateTimeOffset CreatedAtUtc, string Status, IReadOnlyList<RefreshCityDto> Cities);
