using System.Net;
using Microsoft.Extensions.Options;
using WeatherApp.Application.Common;
using WeatherApp.Infrastructure.Weather;

namespace WeatherApp.Tests;

public class WeatherAdapterTests
{
    [Theory]
    [InlineData(401, false)]
    [InlineData(404, false)]
    [InlineData(429, true)]
    [InlineData(503, true)]
    public async Task OnlyTransientHttpFailuresAreRetryable(int status, bool expected)
    {
        var error = await Assert.ThrowsAsync<WeatherServiceException>(() =>
            Service((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))).GetCurrentAsync("Jakarta", "ID", default));
        Assert.Equal(expected, error.IsTransient);
    }
    internal const string Payload = """
        {"name":"Jakarta","sys":{"country":"ID"},"dt":1700000000,"main":{"temp":68,"pressure":1013,"humidity":50},"wind":{"speed":10,"deg":180},"visibility":10000,"weather":[{"main":"Clear"}]}
        """;

    [Fact]
    public async Task MapsImperialPayloadToExplicitWeatherUnits()
    {
        var service = Service((request, _) =>
        {
            Assert.Contains("units=imperial", request.RequestUri!.Query);
            Assert.Contains("q=Jakarta%2CID", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Payload) });
        });
        var result = await service.GetCurrentAsync("Jakarta", "ID", default);
        Assert.Equal("Jakarta", result.City);
        Assert.Equal("ID", result.CountryCode);
        Assert.Equal(68, result.TemperatureFahrenheit);
        Assert.Equal(20, result.TemperatureCelsius);
        Assert.Equal(4.4704, result.WindSpeedMetersPerSecond, 4);
        Assert.Equal(180, result.WindDirectionDegrees);
        Assert.Equal(10000, result.VisibilityMeters);
        Assert.Equal(1013, result.PressureHpa);
        Assert.Equal("Clear", result.SkyConditions);
        Assert.Equal(50, result.RelativeHumidityPercent);
        Assert.InRange(result.DewPointCelsius!.Value, 9.25, 9.27);
        Assert.True(result.DewPointIsCalculated);
        Assert.Equal(1700000000, result.ObservedAtUtc.ToUnixTimeSeconds());
        Assert.Equal(TimeSpan.Zero, result.ObservedAtUtc.Offset);
    }

    [Fact]
    public async Task ZeroHumidityHasUnavailableDewPoint()
    {
        var result = await JsonService(Payload.Replace("\"humidity\":50", "\"humidity\":0")).GetCurrentAsync("Jakarta", "ID", default);
        Assert.Null(result.DewPointCelsius);
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{}")]
    [InlineData("{\"main\":{\"temp\":68}}")]
    public async Task MalformedPayloadsBecomeControlledErrors(string payload) =>
        await Assert.ThrowsAsync<WeatherServiceException>(() => JsonService(payload).GetCurrentAsync("Jakarta", "ID", default));

    [Theory]
    [InlineData(101)]
    [InlineData(-1)]
    public async Task RejectsImpossibleHumidity(int humidity) =>
        await Assert.ThrowsAsync<WeatherServiceException>(() => JsonService(Payload.Replace("\"humidity\":50", $"\"humidity\":{humidity}")).GetCurrentAsync("Jakarta", "ID", default));

    [Fact]
    public async Task EmptySkyConditionsAreAControlledUpstreamFailure()
    {
        var payload = Payload.Replace("[{\"main\":\"Clear\"}]", "[]");
        var error = await Assert.ThrowsAsync<WeatherServiceException>(() => JsonService(payload).GetCurrentAsync("Jakarta", "ID", default));
        Assert.Equal(WeatherFailure.Upstream, error.Failure);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task ProviderErrorsNeverExposeCredentials(int code)
    {
        var ex = await Assert.ThrowsAsync<WeatherServiceException>(() =>
            Service((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)))
                .GetCurrentAsync("Jakarta", "ID", default));
        Assert.DoesNotContain("test-secret", ex.ToString());
    }

    [Fact]
    public async Task DistinguishesTimeoutFromCallerCancellation()
    {
        var service = Service((_, _) => throw new TaskCanceledException());
        var timeout = await Assert.ThrowsAsync<WeatherServiceException>(() => service.GetCurrentAsync("Jakarta", "ID", default));
        Assert.Equal(WeatherFailure.Timeout, timeout.Failure);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetCurrentAsync("Jakarta", "ID", new CancellationToken(true)));
    }

    [Fact]
    public async Task MissingKeyFailsWithoutSendingRequest()
    {
        var service = Service((_, _) => throw new InvalidOperationException("Must not send"), "");
        var ex = await Assert.ThrowsAsync<WeatherServiceException>(() => service.GetCurrentAsync("Jakarta", "ID", default));
        Assert.Equal(WeatherFailure.Configuration, ex.Failure);
    }

    private static OpenWeatherMapService JsonService(string json) => Service((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
    private static OpenWeatherMapService Service(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, string key = "test-secret") =>
        new(new HttpClient(new FakeHttpHandler(send)), Options.Create(new OpenWeatherMapOptions { ApiKey = key }));

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
