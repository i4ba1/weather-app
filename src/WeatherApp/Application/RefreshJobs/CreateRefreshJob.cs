using FluentValidation;
using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Domain;

namespace WeatherApp.Application.RefreshJobs;

public sealed record CreateRefreshJobCommand(IReadOnlyList<RefreshLocationDto?>? Cities) : IRequest<AcceptedRefreshJobDto>;

public sealed class CreateRefreshJobValidator : AbstractValidator<CreateRefreshJobCommand>
{
    public CreateRefreshJobValidator()
    {
        RuleFor(x => x.Cities).NotNull().Must(c => c is { Count: >= 1 and <= 13 })
            .WithMessage("Select between 1 and 13 cities.");
        RuleForEach(x => x.Cities).NotNull();
        RuleForEach(x => x.Cities).ChildRules(city =>
        {
            city.RuleFor(x => x!.CityName).NotEmpty().MaximumLength(100);
            city.RuleFor(x => x!.CountryCode).NotEmpty().Matches("^[a-zA-Z]{2}$");
        });
        RuleFor(x => x.Cities).Must(c => c is null || c.Where(x => x is not null)
            .Select(x => $"{x!.CountryCode}:{x.CityName}".ToUpperInvariant()).Distinct().Count() == c.Count)
            .WithMessage("Cities must be distinct.");
    }
}

public sealed class CreateRefreshJobHandler(ILocationCatalog catalog, IWeatherRefreshRepository repository, TimeProvider clock)
    : IRequestHandler<CreateRefreshJobCommand, AcceptedRefreshJobDto>
{
    public async Task<AcceptedRefreshJobDto> Handle(CreateRefreshJobCommand request, CancellationToken cancellationToken)
    {
        var job = new WeatherRefreshJob(clock.GetUtcNow());
        foreach (var location in request.Cities!)
        {
            var city = catalog.GetCities(location!.CountryCode).FirstOrDefault(c => c.Name.Equals(location.CityName, StringComparison.OrdinalIgnoreCase));
            if (city is null) throw new LocationNotFoundException();
            job.Cities.Add(new WeatherRefreshItem(job.Id, city.Name, city.CountryCode, job.Cities.Count));
        }
        await repository.AddAsync(job, cancellationToken);
        return new AcceptedRefreshJobDto(job.Id);
    }
}
