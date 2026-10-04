namespace WeatherApp.Application.Contracts;

public sealed record CountryDto(string Code, string Name);
public sealed record CityDto(string Name, string CountryCode);
