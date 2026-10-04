using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Contracts;

namespace WeatherApp.Application.Countries;

public sealed record GetCountriesQuery : IRequest<IReadOnlyList<CountryDto>>;

public sealed class GetCountriesHandler(ILocationCatalog catalog) : IRequestHandler<GetCountriesQuery, IReadOnlyList<CountryDto>>
{
    public Task<IReadOnlyList<CountryDto>> Handle(GetCountriesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(catalog.GetCountries());
    }
}
