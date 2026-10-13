using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Infrastructure.Persistence;

namespace WeatherApp.Tests;

public sealed class WeatherApiFactory(WeatherFailure? failure = null, IWeatherService? weatherService = null, string? databasePath = null,
    Action<IServiceCollection>? configureServices = null, int providerIntervalMilliseconds = 0) : WebApplicationFactory<Program>
{
    private readonly string path = databasePath ?? Path.Combine(Path.GetTempPath(), $"weather-test-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WeatherRefresh:DispatcherIntervalMilliseconds"] = "20",
            ["WeatherRefresh:ProviderIntervalMilliseconds"] = providerIntervalMilliseconds.ToString(),
            ["WeatherRefresh:RetryBaseMilliseconds"] = "20",
            ["WeatherRefresh:Capacity"] = "1"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WeatherDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WeatherDbContext>>();
            services.RemoveAll<WeatherDbContext>();
            services.AddDbContext<WeatherDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
            services.RemoveAll<IWeatherService>();
            services.AddSingleton<IWeatherService>(weatherService ?? new ApiWeatherService(failure));
            configureServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && databasePath is null) File.Delete(path);
    }

    private sealed class ApiWeatherService(WeatherFailure? failure) : IWeatherService
    {
        public Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken) =>
            failure.HasValue ? throw new WeatherServiceException(failure.Value)
                : new FakeWeatherService().GetCurrentAsync(cityName, countryCode, cancellationToken);
    }
}
