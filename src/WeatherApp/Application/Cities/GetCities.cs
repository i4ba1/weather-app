using FluentValidation;
using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.Cities;

public sealed record GetCitiesQuery(string CountryCode) : IRequest<IReadOnlyList<CityDto>>;

public sealed class GetCitiesValidator : AbstractValidator<GetCitiesQuery>
{
    public GetCitiesValidator() => RuleFor(x => x.CountryCode).NotEmpty().Matches("^[a-zA-Z]{2}$");
}

public sealed class GetCitiesHandler(ILocationCatalog catalog) : IRequestHandler<GetCitiesQuery, IReadOnlyList<CityDto>>
{
    public Task<IReadOnlyList<CityDto>> Handle(GetCitiesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cities = catalog.GetCities(request.CountryCode);
        if (cities.Count == 0) throw new LocationNotFoundException();
        return Task.FromResult(cities);
    }
}
