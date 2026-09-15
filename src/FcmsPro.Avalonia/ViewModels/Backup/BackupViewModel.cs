using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Backup;
using FcmsPro.Core.Interfaces;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Backup;

/// <summary>
/// Export/import against BackupService, which produces the same JSON shape
/// as the PWA's Backup module (Phase 1 audit §3) - this is also the
/// PWA-data-import entry point mentioned throughout the migration prompt,
/// just targeting SQLite instead of IndexedDB. The `auth` store is always
/// skipped on import (matches PWA behavior, see AuthService remarks) - a
/// PWA-imported backup will NOT carry over the old admin password, so the
/// app still requires the normal first-run admin setup flow separately.
/// </summary>
public partial class BackupViewModel : ObservableObject
{
    private readonly BackupService _backupService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _importAsReplace; // false = Merge (default, safer)
    [ObservableProperty] private DateTimeOffset? _lastBackupAt;
    [ObservableProperty] private bool _autoBackupOnCloseEnabled = true;
    [ObservableProperty] private decimal? _autoBackupKeepCount = 7; // decimal? for NumericUpDown.Value - see TemplateFormViewModel.DeadlineDays for why not int

    public BackupViewModel(BackupService backupService, IUnitOfWork uow, DialogService dialogService)
    {
        _backupService = backupService;
        _uow = uow;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var prefs = await _uow.Settings.GetUiPreferencesAsync();
            LastBackupAt = prefs.LastBackupAt;
            AutoBackupOnCloseEnabled = prefs.AutoBackupOnCloseEnabled;
            AutoBackupKeepCount = prefs.AutoBackupKeepCount;
        }
        catch
        {
            // Non-critical, display-only/default values - if this fails,
            // silently leaving these at their fallback defaults is
            // preferable to surfacing an error banner on a page whose main
            // actions (Export/Import) are otherwise unaffected and already
            // have their own proper error handling below.
        }
    }

    [RelayCommand]
    private async Task SaveAutoBackupSettingsAsync()
    {
        StatusMessage = null;
        try
        {
            var prefs = await _uow.Settings.GetUiPreferencesAsync();
            prefs.AutoBackupOnCloseEnabled = AutoBackupOnCloseEnabled;
            prefs.AutoBackupKeepCount = (int)Math.Max(1, AutoBackupKeepCount ?? 7);
            await _uow.Settings.SaveUiPreferencesAsync(prefs);
            await _uow.SaveChangesAsync();
            StatusMessage = "Auto-backup settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save auto-backup settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        StatusMessage = null;
        var path = await _dialogService.SaveFileAsync(
            $"fcmspro-backup-{DateTime.Now:yyyy-MM-dd}.json", "Save Backup");
        if (path is null) return;

        IsBusy = true;
        try
        {
            var json = await _backupService.ExportAllAsync();
            await System.IO.File.WriteAllTextAsync(path, json);

            var prefs = await _uow.Settings.GetUiPreferencesAsync();
            prefs.LastBackupAt = DateTimeOffset.UtcNow;
            await _uow.Settings.SaveUiPreferencesAsync(prefs);
            await _uow.SaveChangesAsync();
            LastBackupAt = prefs.LastBackupAt;

            StatusMessage = $"Backup saved to {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        StatusMessage = null;

        var confirmed = await _dialogService.ConfirmAsync(
            ImportAsReplace ? "Replace All Data?" : "Merge Backup?",
            ImportAsReplace
                ? "This will REPLACE all current data (clients, commissions, payments, etc.) with the contents of the backup file. This cannot be undone."
                : "This will merge the backup file's records into your current data. Existing records with matching IDs will be overwritten.",
            isDestructive: ImportAsReplace,
            confirmLabel: "Import");
        if (!confirmed) return;

        var path = await _dialogService.OpenFileAsync("Select Backup File", "json");
        if (path is null) return;

        IsBusy = true;
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(path);
            await _backupService.ImportAllAsync(json, ImportAsReplace ? ImportMode.Replace : ImportMode.Merge);
            StatusMessage = "Import complete. Restart the app or navigate away and back to see the new data everywhere.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
