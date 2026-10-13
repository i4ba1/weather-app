using Microsoft.EntityFrameworkCore;
using WeatherApp.Domain;

namespace WeatherApp.Infrastructure.Persistence;

public sealed class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<WeatherNote> Notes => Set<WeatherNote>();
    public DbSet<WeatherRefreshJob> RefreshJobs => Set<WeatherRefreshJob>();
    public DbSet<WeatherRefreshItem> RefreshItems => Set<WeatherRefreshItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var note = modelBuilder.Entity<WeatherNote>();
        note.HasKey(x => x.Id);
        note.Property(x => x.Id).ValueGeneratedNever();
        note.Property(x => x.CityName).HasMaxLength(100).IsRequired();
        note.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        note.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        note.Property(x => x.CreatedAtUtc).IsRequired();
        var job = modelBuilder.Entity<WeatherRefreshJob>();
        job.HasKey(x => x.Id);
        job.Property(x => x.Id).ValueGeneratedNever();
        job.HasMany(x => x.Cities).WithOne().HasForeignKey(x => x.JobId);
        var item = modelBuilder.Entity<WeatherRefreshItem>();
        item.HasKey(x => x.Id);
        item.Property(x => x.Id).ValueGeneratedNever();
        item.Property(x => x.CityName).HasMaxLength(100).IsRequired();
        item.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        item.HasIndex(x => new { x.Status, x.NextAttemptAtUnixMs });
    }
}
