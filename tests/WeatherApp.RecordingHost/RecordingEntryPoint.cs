using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherApp.Application.Abstractions;
using WeatherApp.Infrastructure.Persistence;
using WeatherApp.Infrastructure.Weather;

namespace WeatherApp.RecordingHost;

public static class RecordingEntryPoint
{
    public static async Task Main()
    {
        var artifacts = Path.GetFullPath("../../../../../artifacts/channels-e2e", AppContext.BaseDirectory);
        Directory.CreateDirectory(artifacts);
        var database = Path.Combine(artifacts, $"sample-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(Path.Combine(artifacts, "sample-database.txt"), database);
        var provider = new SampleProviderHandler();
        await using var factory = new RecordingFactory(database, provider);
        factory.UseKestrel(options => options.ListenLocalhost(5105));
        using var client = factory.CreateClient();
        Console.WriteLine("Sample recording API ready on loopback port 5105; external HTTP is replaced by a test handler.");
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) => { args.Cancel = true; shutdown.Cancel(); };
        try { await Task.Delay(Timeout.Infinite, shutdown.Token); }
        catch (OperationCanceledException) { }
    }
}

internal sealed class RecordingFactory(string database, SampleProviderHandler provider) : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath("../../../../../src/WeatherApp", AppContext.BaseDirectory));
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenWeatherMap:ApiKey"] = "synthetic-recording-placeholder",
            ["WeatherRefresh:DispatcherIntervalMilliseconds"] = "100",
            ["WeatherRefresh:ProviderIntervalMilliseconds"] = "300",
            ["WeatherRefresh:RetryBaseMilliseconds"] = "600",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WeatherDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WeatherDbContext>>();
            services.RemoveAll<WeatherDbContext>();
            services.AddDbContext<WeatherDbContext>(options => options.UseSqlite($"Data Source={database};Pooling=False"));
            // Keep the real adapter, mapping, retry classification and all API/application code.
            // The primary handler never opens a network connection.
            services.AddHttpClient<IWeatherService, OpenWeatherMapService>()
                .ConfigurePrimaryHttpMessageHandler(() => provider);
        });
    }
}

internal sealed class SampleProviderHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, int> attempts = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var location = QueryHelpers.ParseQuery(request.RequestUri!.Query)["q"].ToString().Split(',');
        var city = location[0];
        var country = location[1];
        var attempt = attempts.AddOrUpdate(city, 1, (_, prior) => prior + 1);
        await Task.Delay(2000, cancellationToken); // Make concurrent progress readable in the recording.
        if (city == "Surabaya") return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        if (city == "Bandung" && attempt == 1) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var payload = new
        {
            name = city, sys = new { country }, dt = 1700000000,
            main = new { temp = city == "Bandung" ? 71.6 : 68, pressure = 1013, humidity = 50 },
            wind = new { speed = 10, deg = 180 }, visibility = 10000,
            weather = new[] { new { main = "Sample Clear" } }
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload)) };
    }
}
