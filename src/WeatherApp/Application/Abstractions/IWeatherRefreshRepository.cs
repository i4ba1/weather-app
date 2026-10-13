using WeatherApp.Domain;
using WeatherApp.Application.RefreshJobs;

namespace WeatherApp.Application.Abstractions;

public interface IWeatherRefreshRepository
{
    Task AddAsync(WeatherRefreshJob job, CancellationToken token);
    Task<RefreshJobDto?> GetAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<Guid>> GetPendingAsync(long nowUnixMs, int limit, CancellationToken token);
    Task<WeatherRefreshItem?> ClaimAsync(Guid id, long nowUnixMs, CancellationToken token);
    Task SaveAsync(CancellationToken token);
    Task RecoverAsync(long? expiredBeforeUnixMs, int maxAttempts, CancellationToken token);
}
