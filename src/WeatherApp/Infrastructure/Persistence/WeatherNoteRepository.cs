using WeatherApp.Application.Abstractions;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.Persistence;

public sealed class WeatherNoteRepository(WeatherDbContext context) : IWeatherNoteRepository
{
    public async Task AddAsync(WeatherNote note, CancellationToken cancellationToken)
    {
        await context.Notes.AddAsync(note, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
