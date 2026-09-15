using System.Text.Json;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;

namespace FcmsPro.Core.Backup;

public enum ImportMode { Merge, Replace }

/// <summary>
/// Export/import in the same JSON shape as the PWA's Backup module:
/// { _meta: {...}, clients: [...], commissions: [...], ..., settings: "&lt;stringified JSON&gt;" }
/// Confirmed compatible in Phase 1 audit §3 - this is also the entry point for
/// PWA-data import (same format, just targeting SQLite instead of IndexedDB and
/// the AppSettings table instead of localStorage). The `auth` store is always
/// skipped on import, matching PWA behavior - see AuthService remarks.
/// </summary>
public class BackupService
{
    private readonly IUnitOfWork _uow;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public BackupService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Silently writes a timestamped backup into <paramref name="backupsDir"/>
    /// and deletes the oldest files beyond <paramref name="keepCount"/> -
    /// the "keep last N" rotation for UiPreferences.AutoBackupOnCloseEnabled.
    /// Deliberately swallows its own errors rather than throwing: this runs
    /// during app shutdown (MainWindow.Closing), where an unhandled
    /// exception could prevent the window from actually closing at all -
    /// far worse than one skipped backup. Returns the written file path on
    /// success, or null if it failed or was skipped.
    /// </summary>
    public async Task<string?> WriteRotatingBackupAsync(string backupsDir, int keepCount, CancellationToken ct = default)
    {
        try
        {
            System.IO.Directory.CreateDirectory(backupsDir);

            var json = await ExportAllAsync(ct);
            var fileName = $"auto-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var path = System.IO.Path.Combine(backupsDir, fileName);
            await System.IO.File.WriteAllTextAsync(path, json, ct);

            var existing = System.IO.Directory.GetFiles(backupsDir, "auto-*.json")
                .OrderByDescending(f => f)
                .ToList();
            foreach (var stale in existing.Skip(Math.Max(0, keepCount)))
            {
                try { System.IO.File.Delete(stale); }
                catch { /* best-effort cleanup, not worth failing the backup over */ }
            }

            return path;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> ExportAllAsync(CancellationToken ct = default)
    {
        var commissions = await _uow.Commissions.GetAllAsync(ct);
        var payments = new List<Payment>();
        foreach (var c in commissions)
            payments.AddRange(await _uow.Payments.GetByCommissionIdAsync(c.Id, ct));

        var export = new BackupExport
        {
            Meta = new BackupMeta
            {
                ExportedAt = DateTimeOffset.UtcNow,
                Version = 1,
                App = "FcmsPro"
            },
            Clients = await _uow.Clients.GetAllAsync(ct),
            Commissions = commissions,
            Payments = payments,
            Invoices = await _uow.Invoices.GetAllAsync(ct),
            Quotes = await _uow.Quotes.GetAllAsync(ct),
            Expenses = await _uow.Expenses.GetAllAsync(ct),
            Templates = await _uow.Templates.GetAllAsync(ct),
            Goals = await _uow.Settings.GetGoalSettingsAsync(ct),
            SettingsJson = JsonSerializer.Serialize(await _uow.Settings.GetAppSettingsAsync(ct))
            // Note: `auth` intentionally omitted, matching PWA export/import behavior.
        };

        await _uow.AuditLogs.AddAsync(new Entities.AuditLog
        {
            Type = AuditLogType.Backup,
            Message = "Exported full backup"
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public async Task ImportAllAsync(string json, ImportMode mode, CancellationToken ct = default)
    {
        var import = JsonSerializer.Deserialize<BackupExport>(json)
            ?? throw new InvalidOperationException("Backup file could not be read.");

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            await ImportEntitiesAsync(import.Clients, _uow.Clients, mode, ct);
            await ImportEntitiesAsync(import.Commissions, _uow.Commissions, mode, ct);
            await ImportEntitiesAsync(import.Payments, _uow.Payments, mode, ct);
            await ImportEntitiesAsync(import.Invoices, _uow.Invoices, mode, ct);
            await ImportEntitiesAsync(import.Quotes, _uow.Quotes, mode, ct);
            await ImportEntitiesAsync(import.Expenses, _uow.Expenses, mode, ct);
            await ImportEntitiesAsync(import.Templates, _uow.Templates, mode, ct);

            // Settings only restored in Replace mode, never merged - matches PWA behavior.
            if (mode == ImportMode.Replace)
            {
                if (import.Goals is not null)
                    await _uow.Settings.SaveGoalSettingsAsync(import.Goals, ct);

                if (!string.IsNullOrWhiteSpace(import.SettingsJson))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(import.SettingsJson);
                    if (settings is not null)
                        await _uow.Settings.SaveAppSettingsAsync(settings, ct);
                }
            }

            await _uow.AuditLogs.AddAsync(new Entities.AuditLog
            {
                Type = AuditLogType.Restore,
                Message = $"Imported backup ({mode})"
            }, ct);

            await _uow.SaveChangesAsync(ct);
        }, ct);
    }

    private static async Task ImportEntitiesAsync<T>(
        List<T>? incoming,
        IRepository<T> repo,
        ImportMode mode,
        CancellationToken ct) where T : class
    {
        if (incoming is null) return;

        if (mode == ImportMode.Replace)
        {
            foreach (var existing in await repo.GetAllAsync(ct))
                repo.Remove(existing);
        }

        foreach (var entity in incoming)
            await repo.AddAsync(entity, ct);
    }
}

public class BackupMeta
{
    public DateTimeOffset ExportedAt { get; set; }
    public int Version { get; set; }
    public string App { get; set; } = "FcmsPro";
}

public class BackupExport
{
    public BackupMeta Meta { get; set; } = new();
    public List<Client> Clients { get; set; } = new();
    public List<Commission> Commissions { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<Invoice> Invoices { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<CommissionTemplate> Templates { get; set; } = new();
    public GoalSettings? Goals { get; set; }
    public string? SettingsJson { get; set; }
}
