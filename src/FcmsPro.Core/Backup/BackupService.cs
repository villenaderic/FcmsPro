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
/// skipped on import - there's no admin account/login in this app.
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
            // Write to a temp file then move, so a crash/power loss mid-write
            // never leaves a truncated file that looks like a valid backup.
            var tempPath = path + ".tmp";
            await System.IO.File.WriteAllTextAsync(tempPath, json, ct);
            System.IO.File.Move(tempPath, path, overwrite: true);

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
            Expenses = await _uow.Expenses.GetAllAsync(ct),
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
        BackupExport? import;
        try
        {
            import = JsonSerializer.Deserialize<BackupExport>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("That file isn't a valid FCMS Pro backup.", ex);
        }

        if (import is null)
            throw new InvalidOperationException("Backup file could not be read.");
        if (import.Meta is null || !string.Equals(import.Meta.App, "FcmsPro", StringComparison.Ordinal))
            throw new InvalidOperationException("That file wasn't created by FCMS Pro.");
        if (import.Meta.Version > SupportedBackupVersion)
            throw new InvalidOperationException("That backup was created by a newer version of FCMS Pro. Please update the app first.");

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            await ImportEntitiesAsync(import.Clients, _uow.Clients, c => c.Id, mode, ct);
            await ImportEntitiesAsync(import.Commissions, _uow.Commissions, c => c.Id, mode, ct);
            await ImportEntitiesAsync(import.Payments, _uow.Payments, p => p.Id, mode, ct);
            await ImportEntitiesAsync(import.Expenses, _uow.Expenses, e => e.Id, mode, ct);

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

    private const int SupportedBackupVersion = 1;

    private async Task ImportEntitiesAsync<T>(
        List<T>? incoming,
        IRepository<T> repo,
        Func<T, Guid> keyOf,
        ImportMode mode,
        CancellationToken ct) where T : class
    {
        if (incoming is null) return;

        var existing = await repo.GetAllAsync(ct);

        if (mode == ImportMode.Replace)
        {
            foreach (var old in existing)
                repo.Remove(old);

            // Flush the deletes before adding: the incoming rows normally reuse
            // the same primary keys, and EF's change tracker refuses two
            // instances with one key ("cannot be tracked because another
            // instance with the same key value is already being tracked").
            await _uow.SaveChangesAsync(ct);
            existing = new List<T>();
        }

        var existingIds = existing.Select(keyOf).ToHashSet();

        // De-duplicate within the file itself (last occurrence wins) and skip
        // rows with no usable key rather than failing the whole import.
        var unique = new Dictionary<Guid, T>();
        foreach (var entity in incoming)
        {
            var key = keyOf(entity);
            if (key == Guid.Empty) continue;
            unique[key] = entity;
        }

        foreach (var (key, entity) in unique)
        {
            // Merge: a matching ID overwrites the existing record (this is what
            // the import confirmation dialog tells the user will happen)
            // instead of failing with a duplicate-key error.
            if (existingIds.Contains(key))
                repo.Update(entity);
            else
                await repo.AddAsync(entity, ct);
        }
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
    public List<Expense> Expenses { get; set; } = new();
    public GoalSettings? Goals { get; set; }
    public string? SettingsJson { get; set; }
}
