using WeatherApp.Domain;

namespace WeatherApp.Application.Abstractions;

public interface IWeatherNoteRepository
{
    Task AddAsync(WeatherNote note, CancellationToken cancellationToken);
}
