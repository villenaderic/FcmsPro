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

        if (!await db.Templates.AnyAsync(ct))
        {
            foreach (var seed in TemplateService.DefaultTemplates)
            {
                db.Templates.Add(new CommissionTemplate
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    ServiceType = seed.ServiceType,
                    Price = seed.Price,
                    DownPayment = seed.DownPayment,
                    DeadlineDays = seed.DeadlineDays,
                    Description = seed.Description,
                    IsDefault = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        foreach (var counterName in new[] { "quote_seq", "invoice_seq", "receipt_seq" })
        {
            if (!await db.Counters.AnyAsync(c => c.Name == counterName, ct))
                db.Counters.Add(new Counter { Name = counterName, Value = 0 });
        }

        if (!await db.AppSettings.AnyAsync(ct))
            db.AppSettings.Add(new AppSettings { Id = 1 });

        if (!await db.UiPreferences.AnyAsync(ct))
            db.UiPreferences.Add(new UiPreferences { Id = 1 });

        if (!await db.GoalSettings.AnyAsync(ct))
            db.GoalSettings.Add(new GoalSettings { Id = 1 });

        await db.SaveChangesAsync(ct);
    }
}
