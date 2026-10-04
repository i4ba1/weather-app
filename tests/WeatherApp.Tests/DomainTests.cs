using WeatherApp.Domain;

namespace WeatherApp.Tests;

public class DomainTests
{
    [Theory]
    [InlineData(32, 0)]
    [InlineData(212, 100)]
    [InlineData(-40, -40)]
    [InlineData(-4, -20)]
    [InlineData(98.6, 37)]
    public void ConvertsFahrenheit(double input, double expected) =>
        Assert.Equal(expected, Temperature.ToCelsius(input), 6);

    [Fact]
    public void NotesTrimTextAndRequireValidValues()
    {
        var note = new WeatherNote("Jakarta", "ID", "  Clear today  ", DateTimeOffset.UnixEpoch);
        Assert.Equal("Clear today", note.Text);
        Assert.NotEqual(Guid.Empty, note.Id);
        Assert.Throws<ArgumentException>(() => new WeatherNote("Jakarta", "ID", " ", DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new WeatherNote("Jakarta", "ID", new string('x', 2001), DateTimeOffset.UnixEpoch));
    }
}
