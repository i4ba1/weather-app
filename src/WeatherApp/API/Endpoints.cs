using MediatR;
using WeatherApp.Application.Cities;
using WeatherApp.Application.Countries;
using WeatherApp.Application.Notes;
using WeatherApp.Application.Weather;
using WeatherApp.Application.RefreshJobs;

namespace WeatherApp.API;

public static class Endpoints
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapPost("/weather/refresh-jobs", async (CreateRefreshJobCommand command, ISender sender, CancellationToken token) =>
        {
            var job = await sender.Send(command, token);
            return Results.Accepted($"/api/weather/refresh-jobs/{job.Id}", job);
        });
        api.MapGet("/weather/refresh-jobs/{id:guid}", (Guid id, ISender sender, CancellationToken token) =>
            sender.Send(new GetRefreshJobQuery(id), token));
        api.MapGet("/countries", (ISender sender, CancellationToken token) => sender.Send(new GetCountriesQuery(), token));
        api.MapGet("/countries/{countryCode}/cities", (string countryCode, ISender sender, CancellationToken token) =>
            sender.Send(new GetCitiesQuery(countryCode), token));
        api.MapGet("/weather/{cityName}", (string cityName, string? countryCode, ISender sender, CancellationToken token) =>
            sender.Send(new GetWeatherQuery(cityName, countryCode ?? ""), token));
        api.MapPost("/weather/notes", async (CreateWeatherNoteCommand command, ISender sender, CancellationToken token) =>
            Results.Json(await sender.Send(command, token), statusCode: StatusCodes.Status201Created));
    }
}
