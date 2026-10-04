using FluentValidation;
using WeatherApp.Application.Common;
using WeatherApp.Application.Cities;

namespace WeatherApp.Tests;

public class ValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("USA")]
    [InlineData("12")]
    public async Task CountryCodesMustBeTwoLetters(string code)
    {
        var result = await new GetCitiesValidator().ValidateAsync(new GetCitiesQuery(code));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task InvalidRequestCannotReachHandler()
    {
        var behavior = new ValidationBehavior<GetCitiesQuery, IReadOnlyList<WeatherApp.Application.Contracts.CityDto>>([new GetCitiesValidator()]);
        var invoked = false;
        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(new GetCitiesQuery(""), _ =>
        {
            invoked = true;
            return Task.FromResult<IReadOnlyList<WeatherApp.Application.Contracts.CityDto>>([]);
        }, default));
        Assert.False(invoked);
    }
}
