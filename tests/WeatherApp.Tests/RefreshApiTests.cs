using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WeatherApp.Tests;

public class RefreshApiTests
{
    [Fact]
    public async Task AcceptedJobHasPersistedCanonicalCityStatus()
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/weather/refresh-jobs", new
        {
            cities = new[] { new { cityName = "jakarta", countryCode = "id" }, new { cityName = "Bandung", countryCode = "ID" } }
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = accepted.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal($"/api/weather/refresh-jobs/{id}", response.Headers.Location!.OriginalString);
        var job = await client.GetFromJsonAsync<JsonElement>($"/api/weather/refresh-jobs/{id}");
        Assert.Equal(id, job.GetProperty("id").GetGuid());
        Assert.Equal(2, job.GetProperty("cities").GetArrayLength());
        Assert.Equal("Jakarta", job.GetProperty("cities")[0].GetProperty("cityName").GetString());
        Assert.Equal("ID", job.GetProperty("cities")[0].GetProperty("countryCode").GetString());
    }

    [Theory]
    [InlineData("{\"cities\":[]}", 400)]
    [InlineData("{\"cities\":null}", 400)]
    [InlineData("{\"cities\":[null]}", 400)]
    [InlineData("{\"cities\":[{\"cityName\":\"Jakarta\",\"countryCode\":\"USA\"}]}", 400)]
    [InlineData("{\"cities\":[{\"cityName\":\"Unknown\",\"countryCode\":\"ID\"}]}", 404)]
    [InlineData("{\"cities\":[{\"cityName\":\"Jakarta\",\"countryCode\":\"ID\"},{\"cityName\":\"jakarta\",\"countryCode\":\"id\"}]}", 400)]
    public async Task InvalidBatchIsRejected(string json, int expected)
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/weather/refresh-jobs", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task UnknownJobReturns404()
    {
        using var factory = new WeatherApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/weather/refresh-jobs/{Guid.NewGuid()}")).StatusCode);
    }
}
