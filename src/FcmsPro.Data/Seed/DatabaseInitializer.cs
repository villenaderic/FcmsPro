using FcmsPro.Core.Entities;
using FcmsPro.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace FcmsPro.Data.Seed;

/// <summary>
/// Called once at app startup (before the main window shows). Applies pending
/// EF Core migrations, turns on SQLite WAL mode for better concurrent
/// read/write behavior, and seeds first-run data (default templates, zeroed
/// counters) only if the database was just created.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(FcmsDbContext db, CancellationToken ct = default)
    {
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(ct);
        var isFirstRun = !await db.Database.CanConnectAsync(ct) || pendingMigrations.Any();

        await db.Database.MigrateAsync(ct);

        // WAL mode: better read/write concurrency, safer against partial writes
        // on crash/power loss than the default rollback-journal mode.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;", ct); // soft references by design, see Commission/Payment configs





        if (!await db.AppSettings.AnyAsync(ct))
        {
            db.AppSettings.Add(new AppSettings 
            { 
                Id = 1,
                BusinessName = "My Freelance Studio",
                FreelancerName = "John Doe",
                ServiceTypesJson = "[\"Illustration\", \"UI/UX Design\", \"Logo Design\"]",
                PaymentMethodsJson = "[\"Cash\", \"Bank Transfer\", \"PayPal\"]"
            });
        }

        if (!await db.UiPreferences.AnyAsync(ct))
            db.UiPreferences.Add(new UiPreferences { Id = 1 });

        if (!await db.GoalSettings.AnyAsync(ct))
            db.GoalSettings.Add(new GoalSettings { Id = 1 });

        if (!await db.Clients.AnyAsync(ct))
        {
            var sampleClient = new Client
            {
                Id = Guid.NewGuid(),
                Name = "Sample Client (Jane Smith)",
                Email = "jane@example.com",
                DateAdded = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Clients.Add(sampleClient);

            db.Commissions.Add(new Commission
            {
                Id = Guid.NewGuid(),
                ClientId = sampleClient.Id,
                Title = "Sample Logo Design",
                Price = 500,
                DownPayment = 250,
                Deadline = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                Status = FcmsPro.Core.Enums.CommissionStatus.InProgress,
                DateAdded = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
