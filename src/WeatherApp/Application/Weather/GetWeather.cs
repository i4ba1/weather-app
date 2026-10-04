using FluentValidation;
using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.Weather;

public sealed record GetWeatherQuery(string CityName, string CountryCode) : IRequest<WeatherDto>;

public sealed class GetWeatherValidator : AbstractValidator<GetWeatherQuery>
{
    public GetWeatherValidator()
    {
        RuleFor(x => x.CityName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CountryCode).NotEmpty().Matches("^[a-zA-Z]{2}$");
    }
}

public sealed class GetWeatherHandler(ILocationCatalog catalog, IWeatherService service) : IRequestHandler<GetWeatherQuery, WeatherDto>
{
    public Task<WeatherDto> Handle(GetWeatherQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var city = catalog.GetCities(request.CountryCode).FirstOrDefault(city => city.Name.Equals(request.CityName, StringComparison.OrdinalIgnoreCase));
        if (city is null) throw new LocationNotFoundException();
        return service.GetCurrentAsync(city.Name, city.CountryCode, cancellationToken);
    }
}
