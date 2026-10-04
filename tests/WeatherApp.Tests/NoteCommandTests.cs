using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WeatherApp.Application.Common;
using WeatherApp.Application.Contracts;
using WeatherApp.Application.Notes;
using WeatherApp.Infrastructure.Locations;
using WeatherApp.Infrastructure.Persistence;

namespace WeatherApp.Tests;

public class NoteCommandTests
{
    [Fact]
    public async Task SavesCanonicalTrimmedNoteBeforeReturningIdentifier()
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new WeatherDbContext(options);
        var handler = new CreateWeatherNoteHandler(new BundledLocationCatalog(), new WeatherNoteRepository(context), new FixedTime());
        var result = await handler.Handle(new CreateWeatherNoteCommand("jakarta", "id", "  Sunny today  "), default);
        await using var verification = new WeatherDbContext(options);
        var note = await verification.Notes.AsNoTracking().SingleAsync();
        Assert.Equal(result.Id, note.Id);
        Assert.Equal("Jakarta", note.CityName);
        Assert.Equal("ID", note.CountryCode);
        Assert.Equal("Sunny today", note.Text);
        Assert.Equal(DateTimeOffset.UnixEpoch, result.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InvalidNoteCannotBePersisted(string? text)
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new WeatherDbContext(options);
        var handler = new CreateWeatherNoteHandler(new BundledLocationCatalog(), new WeatherNoteRepository(context), TimeProvider.System);
        var behavior = new ValidationBehavior<CreateWeatherNoteCommand, CreatedNoteDto>([new CreateWeatherNoteValidator()]);
        var command = new CreateWeatherNoteCommand("Jakarta", "ID", text!);
        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(command, _ => handler.Handle(command, default), default));
        Assert.Empty(await context.Notes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UnknownCityAndOverlongNoteCreateNoRecord()
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new WeatherDbContext(options);
        var handler = new CreateWeatherNoteHandler(new BundledLocationCatalog(), new WeatherNoteRepository(context), TimeProvider.System);
        await Assert.ThrowsAsync<LocationNotFoundException>(() => handler.Handle(new("Unknown", "ID", "Test"), default));
        Assert.False((await new CreateWeatherNoteValidator().ValidateAsync(new CreateWeatherNoteCommand("Jakarta", "ID", new string('x', 2001)))).IsValid);
        Assert.Empty(await context.Notes.AsNoTracking().ToListAsync());
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
