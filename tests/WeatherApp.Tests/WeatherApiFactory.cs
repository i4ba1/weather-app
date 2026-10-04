using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Infrastructure.Persistence;

namespace WeatherApp.Tests;

public sealed class WeatherApiFactory(WeatherFailure? failure = null) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            connection.Open();
            services.RemoveAll<DbContextOptions<WeatherDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WeatherDbContext>>();
            services.RemoveAll<WeatherDbContext>();
            services.AddDbContext<WeatherDbContext>(options => options.UseSqlite(connection));
            services.RemoveAll<IWeatherService>();
            services.AddSingleton<IWeatherService>(new ApiWeatherService(failure));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    private sealed class ApiWeatherService(WeatherFailure? failure) : IWeatherService
    {
        public Task<WeatherDto> GetCurrentAsync(string cityName, string countryCode, CancellationToken cancellationToken) =>
            failure.HasValue ? throw new WeatherServiceException(failure.Value)
                : new FakeWeatherService().GetCurrentAsync(cityName, countryCode, cancellationToken);
    }
}
