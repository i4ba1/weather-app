namespace WeatherApp.Application.Contracts;

public sealed record CreatedNoteDto(Guid Id, DateTimeOffset CreatedAtUtc);
