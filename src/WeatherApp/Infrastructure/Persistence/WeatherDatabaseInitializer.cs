using Microsoft.EntityFrameworkCore;

namespace WeatherApp.Infrastructure.Persistence;

public static class WeatherDatabaseInitializer
{
    public static async Task InitializeAsync(WeatherDbContext context, CancellationToken token = default)
    {
        await context.Database.EnsureCreatedAsync(token);
        // EnsureCreated does not evolve existing databases. This additive, idempotent
        // upgrade preserves the original Notes table and uses the same EF mapping.
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RefreshJobs" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshJobs" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS "RefreshItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshItems" PRIMARY KEY,
                "JobId" TEXT NOT NULL, "CityName" TEXT NOT NULL, "CountryCode" TEXT NOT NULL,
                "Position" INTEGER NOT NULL, "Status" INTEGER NOT NULL, "Attempts" INTEGER NOT NULL,
                "NextAttemptAtUnixMs" INTEGER NOT NULL, "StartedAtUnixMs" INTEGER NULL,
                "WeatherJson" TEXT NULL, "Error" TEXT NULL,
                CONSTRAINT "FK_RefreshItems_RefreshJobs_JobId" FOREIGN KEY ("JobId") REFERENCES "RefreshJobs" ("Id") ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS "IX_RefreshItems_JobId" ON "RefreshItems" ("JobId");
            CREATE INDEX IF NOT EXISTS "IX_RefreshItems_Status_NextAttemptAtUnixMs" ON "RefreshItems" ("Status", "NextAttemptAtUnixMs");
            """, token);
    }
}
