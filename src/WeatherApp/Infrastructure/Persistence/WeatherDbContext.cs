using Microsoft.EntityFrameworkCore;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.Persistence;

public sealed class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<WeatherNote> Notes => Set<WeatherNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var note = modelBuilder.Entity<WeatherNote>();
        note.HasKey(x => x.Id);
        note.Property(x => x.Id).ValueGeneratedNever();
        note.Property(x => x.CityName).HasMaxLength(100).IsRequired();
        note.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        note.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        note.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
