using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;

namespace FcmsPro.Avalonia.ViewModels.Trash;

/// <summary>
/// Everything currently soft-deleted (Client, Commission, Payment, Expense,
/// Quote, Invoice - see ISoftDeletable) in one flat list, with Restore and
/// Delete Permanently per item. Reachable from Settings &gt; Advanced, same
/// treatment as Logs - not an everyday page, but should be easy to find
/// when needed. Anything left here for 30+ days is auto-purged at the next
/// app startup (TrashService.PurgeExpiredAsync), not from this page.
/// </summary>
public partial class TrashViewModel : ObservableObject
{
    private readonly TrashService _trashService;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<TrashItem> Items { get; } = new();

    public bool HasNoItems => !IsLoading && Items.Count == 0;

    public TrashViewModel(TrashService trashService, NavigationService navigation, DialogService dialogService)
    {
        _trashService = trashService;
        _navigation = navigation;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _trashService.GetAllAsync();
            Items.Clear();
            foreach (var i in items)
                Items.Add(i);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load trash: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoItems));
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(TrashItem? item)
    {
        if (item is null) return;
        ErrorMessage = null;
        try
        {
            await _trashService.RestoreAsync(item.Type, item.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not restore: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PermanentlyDeleteAsync(TrashItem? item)
    {
        if (item is null) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "Permanently Delete?",
            $"\"{item.DisplayName}\" will be permanently deleted. This cannot be undone.",
            isDestructive: true,
            confirmLabel: "Delete Forever");
        if (!confirmed) return;

        ErrorMessage = null;
        try
        {
            await _trashService.PermanentlyDeleteAsync(item.Type, item.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not permanently delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BackToSettings() => _navigation.NavigateTo(AppPage.Settings);
}
