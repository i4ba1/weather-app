using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Infrastructure.Persistence;

namespace WeatherApp.Tests;

public class ApiTests
{
    [Fact]
    public async Task CountryCityAndWeatherRoutesReturnExpectedContracts()
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        var countries = await client.GetFromJsonAsync<CountryDto[]>("/api/countries");
        Assert.Contains(countries!, c => c.Code == "ID" && c.Name == "Indonesia");
        var cities = await client.GetFromJsonAsync<CityDto[]>("/api/countries/id/cities");
        Assert.Contains(cities!, c => c.Name == "Jakarta" && c.CountryCode == "ID");
        using var response = await client.GetAsync("/api/weather/jakarta?countryCode=id");
        response.EnsureSuccessStatusCode();
        var weather = await response.Content.ReadFromJsonAsync<WeatherDto>();
        Assert.Equal("Jakarta", weather!.City);
        Assert.Equal(68, weather.TemperatureFahrenheit);
        Assert.Equal(20, weather.TemperatureCelsius);
        Assert.NotNull(weather.DewPointCelsius);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("observedAtUtc", out _));
        Assert.Equal(13, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task NotePostPersistsAndReturnsCreatedIdentifier()
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/weather/notes", new { cityName = "Jakarta", countryCode = "ID", text = "  Warm  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreatedNoteDto>();
        Assert.NotEqual(Guid.Empty, result!.Id);
        using var scope = factory.Services.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Notes.AsNoTracking().SingleAsync();
        Assert.Equal(result.Id, record.Id);
        Assert.Equal("Warm", record.Text);
        Assert.Equal(TimeSpan.Zero, result.CreatedAtUtc.Offset);
    }

    [Theory]
    [InlineData("/api/countries/USA/cities", 400)]
    [InlineData("/api/countries/XX/cities", 404)]
    [InlineData("/api/weather/Jakarta", 400)]
    [InlineData("/api/weather/Unknown?countryCode=ID", 404)]
    [InlineData("/api/missing", 404)]
    public async Task ErrorsAreJsonProblemDetails(string route, int status)
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(route);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"cityName\":\"Jakarta\",\"countryCode\":\"ID\",\"text\":\"\"}")]
    [InlineData("{}")]
    public async Task MalformedOrInvalidPostsReturn400WithoutInserts(string payload)
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/weather/notes", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Notes.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(WeatherFailure.Upstream, 502)]
    [InlineData(WeatherFailure.Configuration, 503)]
    [InlineData(WeatherFailure.Timeout, 504)]
    public async Task ProviderFailuresHaveStableStatuses(WeatherFailure failure, int status)
    {
        using var factory = new WeatherApiFactory(failure);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/weather/Jakarta?countryCode=ID");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
