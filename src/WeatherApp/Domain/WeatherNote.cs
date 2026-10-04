namespace WeatherApp.Domain;

public sealed class WeatherNote
{
    private WeatherNote() { }

    public WeatherNote(string cityName, string countryCode, string text, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(cityName) || cityName.Trim().Length > 100)
            throw new ArgumentException("City is required and must contain at most 100 characters.", nameof(cityName));
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Trim().Length != 2 || !countryCode.Trim().All(char.IsAsciiLetter))
            throw new ArgumentException("Country code must contain two letters.", nameof(countryCode));
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 2000)
            throw new ArgumentException("Note must contain between 1 and 2000 characters.", nameof(text));
        Id = Guid.NewGuid();
        CityName = cityName.Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        Text = text.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public string CityName { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public string Text { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
