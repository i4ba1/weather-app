using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Application.RefreshJobs;
using WeatherApp.Domain;
using WeatherApp.Infrastructure.Persistence;

namespace WeatherApp.Tests;

public class RefreshWorkerTests
{
    [Fact]
    public async Task DelayedDatabaseClaimsCannotBunchProviderCallStarts()
    {
        var releaseClaims = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstClaim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new System.Collections.Concurrent.ConcurrentQueue<long>();
        using var factory = new WeatherApiFactory(providerIntervalMilliseconds: 150,
            weatherService: new DelegateWeatherService((city, country, _) =>
            {
                starts.Enqueue(System.Diagnostics.Stopwatch.GetTimestamp());
                return Task.FromResult(Sample(city, country));
            }), configureServices: services => services.AddScoped<IWeatherRefreshRepository>(provider =>
                new DelayedClaimRepository(new WeatherRefreshRepository(provider.GetRequiredService<WeatherDbContext>()), firstClaim, releaseClaims)));
        using var client = factory.CreateClient();
        var id = await Submit(client, "Jakarta", "Bandung");
        await firstClaim.Task.WaitAsync(TimeSpan.FromSeconds(4));
        // Hold claims past multiple pacing slots, as SQLite contention could do.
        await Task.Delay(400);
        releaseClaims.TrySetResult();
        Assert.Equal("Completed", (await WaitForFinished(client, id)).Status);
        var times = starts.ToArray();
        Assert.Equal(2, times.Length);
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(times[0], times[1]).TotalMilliseconds >= 140,
            "Actual provider starts must retain the configured interval after delayed claims.");
    }

    [Fact]
    public async Task TwoCitiesExecuteConcurrentlyAndPersistSnapshots()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = new DelegateWeatherService(async (city, country, token) =>
        {
            if (Interlocked.Increment(ref calls) == 2) entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Sample(city, country);
        });
        using var factory = new WeatherApiFactory(weatherService: service);
        using var client = factory.CreateClient();
        var id = await Submit(client, "Jakarta", "Bandung");
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(4)); }
        finally { release.TrySetResult(); }
        var result = await WaitForFinished(client, id);
        Assert.Equal("Completed", result.Status);
        Assert.All(result.Cities, city => { Assert.Equal(1, city.Attempts); Assert.Equal(city.CityName, city.Weather!.City); });
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task TimeoutRetriesButPermanentFailureDoesNotBlockOtherCity()
    {
        var attempts = 0;
        using var factory = new WeatherApiFactory(weatherService: new DelegateWeatherService((city, country, _) =>
        {
            if (city == "Bandung") throw new WeatherServiceException(WeatherFailure.Configuration);
            if (Interlocked.Increment(ref attempts) < 3) throw new WeatherServiceException(WeatherFailure.Timeout);
            return Task.FromResult(Sample(city, country));
        }));
        using var client = factory.CreateClient();
        var result = await WaitForFinished(client, await Submit(client, "Jakarta", "Bandung"));
        Assert.Equal("CompletedWithErrors", result.Status);
        Assert.Equal(3, result.Cities[0].Attempts);
        Assert.NotNull(result.Cities[0].Weather);
        Assert.Equal(1, result.Cities[1].Attempts);
        Assert.Null(result.Cities[1].Weather);
        Assert.Contains("not configured", result.Cities[1].Error);
    }

    [Fact]
    public async Task ShutdownAndRestartRecoverAcceptedJobWithoutBrowser()
    {
        var path = Path.Combine(Path.GetTempPath(), $"refresh-recovery-{Guid.NewGuid()}.db");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid id;
        try
        {
            using (var first = new WeatherApiFactory(databasePath: path, weatherService: new DelegateWeatherService(async (_, _, token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException();
            })))
            {
                using var client = first.CreateClient();
                id = await Submit(client, "Jakarta");
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(4));
            }
            using var second = new WeatherApiFactory(databasePath: path);
            using var secondClient = second.CreateClient();
            var result = await WaitForFinished(secondClient, id);
            Assert.Equal("Completed", result.Status);
            Assert.Equal(2, result.Cities.Single().Attempts);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExhaustedTimeoutFailsAfterThreeAttempts()
    {
        using var factory = new WeatherApiFactory(failure: WeatherFailure.Timeout);
        using var client = factory.CreateClient();
        var result = await WaitForFinished(client, await Submit(client, "Jakarta"));
        Assert.Equal("Failed", result.Status);
        Assert.Equal(3, result.Cities.Single().Attempts);
        Assert.Null(result.Cities.Single().Weather);
    }

    [Fact]
    public async Task ExistingNotesSurviveAdditiveSchemaUpgradeAndStatusReadsDoNotWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"refresh-upgrade-{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<WeatherDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            await using (var context = new WeatherDbContext(options))
            {
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE Notes (Id TEXT PRIMARY KEY, CityName TEXT NOT NULL, CountryCode TEXT NOT NULL, Text TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL);
                    """);
                context.Notes.Add(new WeatherNote("Jakarta", "ID", "Preserve me", DateTimeOffset.UnixEpoch));
                await context.SaveChangesAsync();
                await WeatherDatabaseInitializer.InitializeAsync(context);
                Assert.Equal("Preserve me", (await context.Notes.AsNoTracking().SingleAsync()).Text);
                var job = new WeatherRefreshJob(DateTimeOffset.UnixEpoch);
                job.Cities.Add(new WeatherRefreshItem(job.Id, "Jakarta", "ID", 0));
                await new WeatherRefreshRepository(context).AddAsync(job, default);
                context.ChangeTracker.Clear();
                var repository = new WeatherRefreshRepository(context);
                var before = await repository.GetAsync(job.Id, default);
                var after = await repository.GetAsync(job.Id, default);
                Assert.Equal(before!.Status, after!.Status);
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RecoveryPreservesCompletedSnapshotAndFailsExhaustedInterruptedWork()
    {
        var path = Path.Combine(Path.GetTempPath(), $"refresh-limit-{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<WeatherDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var context = new WeatherDbContext(options);
            await WeatherDatabaseInitializer.InitializeAsync(context);
            var job = new WeatherRefreshJob(DateTimeOffset.UnixEpoch);
            var complete = new WeatherRefreshItem(job.Id, "Jakarta", "ID", 0);
            var interrupted = new WeatherRefreshItem(job.Id, "Bandung", "ID", 1);
            job.Cities.AddRange([complete, interrupted]);
            var repository = new WeatherRefreshRepository(context);
            await repository.AddAsync(job, default);
            context.ChangeTracker.Clear();
            var completedClaim = await repository.ClaimAsync(complete.Id, 0, default);
            completedClaim!.Complete(System.Text.Json.JsonSerializer.Serialize(Sample("Jakarta", "ID")));
            await repository.SaveAsync(default);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                context.ChangeTracker.Clear();
                Assert.NotNull(await repository.ClaimAsync(interrupted.Id, 0, default));
                await repository.RecoverAsync(null, 3, default);
            }
            var result = (await repository.GetAsync(job.Id, default))!;
            Assert.Equal("CompletedWithErrors", result.Status);
            Assert.Equal("Completed", result.Cities[0].Status);
            Assert.Equal(1, result.Cities[0].Attempts);
            Assert.Equal(20, result.Cities[0].Weather!.TemperatureCelsius);
            Assert.Equal("Failed", result.Cities[1].Status);
            Assert.Equal(3, result.Cities[1].Attempts);
            Assert.Contains("attempt limit", result.Cities[1].Error);
        }
        finally { File.Delete(path); }
    }

    internal static WeatherDto Sample(string city, string country) => new(city, country, DateTimeOffset.UnixEpoch,
        4.4704, 180, 10000, 1013, "Synthetic test fixture", 68, 20, 9.26, true, 50);
    internal static async Task<Guid> Submit(HttpClient client, params string[] cities)
    {
        using var response = await client.PostAsJsonAsync("/api/weather/refresh-jobs", new { cities = cities.Select(city => new { cityName = city, countryCode = "ID" }) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AcceptedRefreshJobDto>())!.Id;
    }
    internal static async Task<RefreshJobDto> WaitForFinished(HttpClient client, Guid id)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        while (true)
        {
            var job = (await client.GetFromJsonAsync<RefreshJobDto>($"/api/weather/refresh-jobs/{id}", deadline.Token))!;
            if (job.Status is "Completed" or "Failed" or "CompletedWithErrors") return job;
            await Task.Delay(20, deadline.Token);
        }
    }
    private sealed class DelegateWeatherService(Func<string, string, CancellationToken, Task<WeatherDto>> run) : IWeatherService
    {
        public Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken) => run(cityName, countryCode, cancellationToken);
    }
    private sealed class DelayedClaimRepository(IWeatherRefreshRepository inner, TaskCompletionSource entered, TaskCompletionSource release) : IWeatherRefreshRepository
    {
        public Task AddAsync(WeatherRefreshJob job, CancellationToken token) => inner.AddAsync(job, token);
        public Task<RefreshJobDto?> GetAsync(Guid id, CancellationToken token) => inner.GetAsync(id, token);
        public Task<IReadOnlyList<Guid>> GetPendingAsync(long now, int limit, CancellationToken token) => inner.GetPendingAsync(now, limit, token);
        public async Task<WeatherRefreshItem?> ClaimAsync(Guid id, long now, CancellationToken token)
        {
            entered.TrySetResult(); await release.Task.WaitAsync(token);
            return await inner.ClaimAsync(id, now, token);
        }
        public Task SaveAsync(CancellationToken token) => inner.SaveAsync(token);
        public Task RecoverAsync(long? before, int max, CancellationToken token) => inner.RecoverAsync(before, max, token);
    }
}
