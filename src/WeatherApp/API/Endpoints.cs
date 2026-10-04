using MediatR;
using WeatherApp.Application.Cities;
using WeatherApp.Application.Countries;
using WeatherApp.Application.Notes;
using WeatherApp.Application.Weather;

namespace WeatherApp.API;

public static class Endpoints
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/countries", (ISender sender, CancellationToken token) => sender.Send(new GetCountriesQuery(), token));
        api.MapGet("/countries/{countryCode}/cities", (string countryCode, ISender sender, CancellationToken token) =>
            sender.Send(new GetCitiesQuery(countryCode), token));
        api.MapGet("/weather/{cityName}", (string cityName, string? countryCode, ISender sender, CancellationToken token) =>
            sender.Send(new GetWeatherQuery(cityName, countryCode ?? ""), token));
        api.MapPost("/weather/notes", async (CreateWeatherNoteCommand command, ISender sender, CancellationToken token) =>
            Results.Json(await sender.Send(command, token), statusCode: StatusCodes.Status201Created));
    }
}
