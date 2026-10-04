using FluentValidation;
using MediatR;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Domain;

namespace WeatherApp.Application.Notes;

public sealed record CreateWeatherNoteCommand(string CityName, string CountryCode, string Text) : IRequest<CreatedNoteDto>;

public sealed class CreateWeatherNoteValidator : AbstractValidator<CreateWeatherNoteCommand>
{
    public CreateWeatherNoteValidator()
    {
        RuleFor(x => x.CityName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CountryCode).NotEmpty().Matches("^[a-zA-Z]{2}$");
        RuleFor(x => x.Text).NotEmpty().Must(text => text is null || text.Trim().Length <= 2000)
            .WithMessage("Note must contain at most 2000 characters.");
    }
}

public sealed class CreateWeatherNoteHandler(ILocationCatalog catalog, IWeatherNoteRepository repository, TimeProvider clock)
    : IRequestHandler<CreateWeatherNoteCommand, CreatedNoteDto>
{
    public async Task<CreatedNoteDto> Handle(CreateWeatherNoteCommand request, CancellationToken cancellationToken)
    {
        var city = catalog.GetCities(request.CountryCode).FirstOrDefault(c => c.Name.Equals(request.CityName, StringComparison.OrdinalIgnoreCase));
        if (city is null) throw new LocationNotFoundException();
        var note = new WeatherNote(city.Name, city.CountryCode, request.Text, clock.GetUtcNow());
        await repository.AddAsync(note, cancellationToken);
        return new CreatedNoteDto(note.Id, note.CreatedAtUtc);
    }
}
