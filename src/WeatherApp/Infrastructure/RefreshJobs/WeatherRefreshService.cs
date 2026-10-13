using System.Text.Json;
using Microsoft.Extensions.Options;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.RefreshJobs;

public sealed class WeatherRefreshService(IServiceScopeFactory scopes, WeatherRefreshQueue queue,
    IOptions<WeatherRefreshOptions> options, TimeProvider clock, ILogger<WeatherRefreshService> logger) : BackgroundService
{
    private readonly WeatherRefreshOptions settings = options.Value;
    private readonly SemaphoreSlim providerStart = new(1, 1);
    private long nextProviderStart;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A single application owns this database. Recover persisted work before dispatch.
        await RecoverAsync(null, stoppingToken);
        await Task.WhenAll(Enumerable.Range(0, settings.Workers).Select(_ => WorkAsync(stoppingToken))
            .Append(DispatchAsync(stoppingToken)));
    }

    private async Task DispatchAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Also recovers an item stranded by a database failure during a live run.
                await RecoverAsync(clock.GetUtcNow().AddMinutes(-2).ToUnixTimeMilliseconds(), token);
                IReadOnlyList<Guid> ids;
                await using (var scope = scopes.CreateAsyncScope())
                    ids = await scope.ServiceProvider.GetRequiredService<IWeatherRefreshRepository>()
                        .GetPendingAsync(clock.GetUtcNow().ToUnixTimeMilliseconds(), settings.Capacity + settings.Workers, token);
                foreach (var id in ids) await queue.EnqueueAsync(id, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Refresh dispatcher failed; pending work will be retried."); }
            await Task.Delay(TimeSpan.FromMilliseconds(settings.DispatcherIntervalMilliseconds), clock, token);
        }
    }

    private async Task WorkAsync(CancellationToken token)
    {
        await foreach (var id in queue.ReadAllAsync(token))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IWeatherRefreshRepository>();
                var (item, response) = await ClaimAndStartAsync(repository,
                    scope.ServiceProvider.GetRequiredService<IWeatherService>(), id, token);
                if (item is null) continue;
                try
                {
                    var weather = await response!;
                    item.Complete(JsonSerializer.Serialize(weather));
                }
                catch (WeatherServiceException failure)
                {
                    var retry = failure.IsTransient && item.Attempts < settings.MaxAttempts;
                    var delay = settings.RetryBaseMilliseconds * Math.Pow(2, item.Attempts - 1);
                    item.Fail(failure.Message, retry, clock.GetUtcNow().AddMilliseconds(delay).ToUnixTimeMilliseconds());
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // Never persist raw exceptions: HTTP exceptions may carry credential-bearing URIs.
                    item.Fail("Weather refresh failed unexpectedly.", false, 0);
                }
                await repository.SaveAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Refresh item {ItemId} could not be persisted; recovery will retry it.", id); }
            finally { queue.Complete(id); }
        }
    }

    private async Task<(WeatherRefreshItem? Item, Task<WeatherDto>? Response)> ClaimAndStartAsync(
        IWeatherRefreshRepository repository, IWeatherService service, Guid id, CancellationToken token)
    {
        await providerStart.WaitAsync(token);
        try
        {
            var wait = nextProviderStart - clock.GetUtcNow().ToUnixTimeMilliseconds();
            if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), clock, token);
            // Keep the gate through the claim: database delays must not bunch HTTP starts.
            var item = await repository.ClaimAsync(id, clock.GetUtcNow().ToUnixTimeMilliseconds(), token);
            if (item is null) return (null, null);
            nextProviderStart = clock.GetUtcNow().ToUnixTimeMilliseconds() + settings.ProviderIntervalMilliseconds;
            try { return (item, service.GetCurrentAsync(item.CityName, item.CountryCode, token)); }
            catch (Exception exception) { return (item, Task.FromException<WeatherDto>(exception)); }
        }
        finally { providerStart.Release(); }
    }

    private async Task RecoverAsync(long? expiredBefore, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWeatherRefreshRepository>().RecoverAsync(expiredBefore, settings.MaxAttempts, token);
    }
    public override void Dispose() { providerStart.Dispose(); base.Dispose(); }
}
