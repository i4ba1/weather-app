using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeatherApp.Application.Abstractions;
using WeatherApp.Application.Common;
using WeatherApp.Application.Countries;
using WeatherApp.Infrastructure.Locations;
using WeatherApp.Infrastructure.Persistence;
using WeatherApp.Infrastructure.Weather;

namespace WeatherApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWeatherApplication(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<GetCountriesQuery>();
            options.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblyContaining<GetCountriesQuery>();
        services.AddSingleton<ILocationCatalog, BundledLocationCatalog>();
        services.AddSingleton(TimeProvider.System);
        services.Configure<OpenWeatherMapOptions>(configuration.GetSection("OpenWeatherMap"));
        services.AddHttpClient<IWeatherService, OpenWeatherMapService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 1024 * 1024;
        }).RemoveAllLoggers(); // Provider URIs contain the API key.

        var connection = new SqliteConnectionStringBuilder(configuration.GetConnectionString("Weather")
            ?? (environment.IsEnvironment("Testing") ? "Data Source=:memory:" : "Data Source=data/weather.db"));
        if (connection.DataSource != ":memory:")
        {
            connection.DataSource = Path.GetFullPath(connection.DataSource, environment.ContentRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
        }
        services.AddDbContext<WeatherDbContext>(options => options.UseSqlite(connection.ToString()));
        services.AddScoped<IWeatherNoteRepository, WeatherNoteRepository>();
        return services;
    }
}
