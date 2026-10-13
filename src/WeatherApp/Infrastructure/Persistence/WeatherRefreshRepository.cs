using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Contracts;
using WeatherApp.Application.RefreshJobs;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.Persistence;

public sealed class WeatherRefreshRepository(WeatherDbContext context) : IWeatherRefreshRepository
{
    public async Task AddAsync(WeatherRefreshJob job, CancellationToken token)
    {
        context.RefreshJobs.Add(job);
        await context.SaveChangesAsync(token);
    }
    public async Task<RefreshJobDto?> GetAsync(Guid id, CancellationToken token)
    {
        var job = await context.RefreshJobs.AsNoTracking().Include(x => x.Cities).SingleOrDefaultAsync(x => x.Id == id, token);
        if (job is null) return null;
        var cities = job.Cities.OrderBy(x => x.Position).Select(x => new RefreshCityDto(x.CityName, x.CountryCode,
            x.Status.ToString(), x.Attempts, x.WeatherJson is null ? null : JsonSerializer.Deserialize<WeatherDto>(x.WeatherJson), x.Error)).ToArray();
        var status = cities.Any(x => x.Status == "Running") ? "Running"
            : cities.Any(x => x.Status == "Pending") ? "Pending"
            : cities.All(x => x.Status == "Completed") ? "Completed"
            : cities.All(x => x.Status == "Failed") ? "Failed" : "CompletedWithErrors";
        return new RefreshJobDto(job.Id, job.CreatedAtUtc, status, cities);
    }
    public async Task<IReadOnlyList<Guid>> GetPendingAsync(long nowUnixMs, int limit, CancellationToken token) =>
        await context.RefreshItems.AsNoTracking().Where(x => x.Status == RefreshStatus.Pending && x.NextAttemptAtUnixMs <= nowUnixMs)
            .OrderBy(x => x.NextAttemptAtUnixMs).ThenBy(x => x.Id).Take(limit).Select(x => x.Id).ToArrayAsync(token);

    public async Task<WeatherRefreshItem?> ClaimAsync(Guid id, long nowUnixMs, CancellationToken token)
    {
        var changed = await context.RefreshItems.Where(x => x.Id == id && x.Status == RefreshStatus.Pending && x.NextAttemptAtUnixMs <= nowUnixMs)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, RefreshStatus.Running)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1).SetProperty(x => x.StartedAtUnixMs, (long?)nowUnixMs), token);
        return changed == 0 ? null : await context.RefreshItems.SingleAsync(x => x.Id == id, token);
    }
    public Task SaveAsync(CancellationToken token) => context.SaveChangesAsync(token);

    public async Task RecoverAsync(long? expiredBeforeUnixMs, int maxAttempts, CancellationToken token)
    {
        var interrupted = context.RefreshItems.Where(x => x.Status == RefreshStatus.Running
            && (expiredBeforeUnixMs == null || x.StartedAtUnixMs < expiredBeforeUnixMs));
        await interrupted.Where(x => x.Attempts >= maxAttempts).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, RefreshStatus.Failed).SetProperty(x => x.Error, "Refresh interrupted; attempt limit reached.")
            .SetProperty(x => x.StartedAtUnixMs, (long?)null), token);
        await interrupted.Where(x => x.Attempts < maxAttempts).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, RefreshStatus.Pending).SetProperty(x => x.NextAttemptAtUnixMs, 0L)
            .SetProperty(x => x.StartedAtUnixMs, (long?)null), token);
    }
}
