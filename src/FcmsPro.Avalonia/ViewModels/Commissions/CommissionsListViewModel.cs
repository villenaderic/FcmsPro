using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Core.Entities;
using FcmsPro.Core.Enums;
using FcmsPro.Core.Interfaces;
using FcmsPro.Core.Services;
using FcmsPro.Avalonia.Services;
using FcmsPro.Avalonia.ViewModels.Shell;

namespace FcmsPro.Avalonia.ViewModels.Commissions;

/// <summary>
/// Table view of Commissions (a Kanban board alternate view previously
/// lived alongside this - removed as the higher-bug-surface, lower-value of
/// the two views; Table covers the same data and status-change workflow).
/// </summary>
public partial class CommissionsListViewModel : ObservableObject, ICreatablePage
{
    private readonly CommissionService _commissionService;
    private readonly IUnitOfWork _uow;
    private readonly DialogService _dialogService;
    private readonly NavigationService _navigation;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private CommissionStatus? _statusFilter; // null = "All"
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Client? _filteredByClient; // set when deep-linked from a Client profile
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private ListSortOption _sortOption = ListSortOption.Newest;

    /// <summary>Sort choices that make sense for a Commission: it has a price and an optional deadline, so both are offered alongside the name/date basics.</summary>
    public IReadOnlyList<ListSortOption> SortOptions { get; } = new[]
    {
        ListSortOption.Newest, ListSortOption.Oldest,
        ListSortOption.AmountHighToLow, ListSortOption.AmountLowToHigh,
        ListSortOption.DeadlineSoonest, ListSortOption.NameAZ
    };

    partial void OnSortOptionChanged(ListSortOption value) => ApplyFilterToRows();

    public ObservableCollection<CommissionRowViewModel> Rows { get; } = new();

    /// <summary>True once a load has completed and found nothing - drives the empty-state illustration in CommissionsListView.</summary>
    public bool HasNoResults => !IsLoading && Rows.Count == 0;
    /// <summary>Includes a null "All" entry for the filter dropdown at the top of the page - not used by individual row/card ComboBoxes, which use CommissionRowViewModel.StatusOptions instead.</summary>
    public IReadOnlyList<CommissionStatus?> StatusFilterOptions { get; } =
        new CommissionStatus?[] { null }.Concat(Enum.GetValues<CommissionStatus>().Cast<CommissionStatus?>()).ToList();

    public CommissionsListViewModel(CommissionService commissionService, IUnitOfWork uow, DialogService dialogService, NavigationService navigation)
    {
        _commissionService = commissionService;
        _uow = uow;
        _dialogService = dialogService;
        _navigation = navigation;

        if (navigation.NavigationParameter is Guid clientId)
            _ = LoadForClientAsync(clientId);
        else
            _ = LoadAsync();
    }

    private async Task LoadForClientAsync(Guid clientId)
    {
        try
        {
            FilteredByClient = await _uow.Clients.GetByIdAsync(clientId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load client filter: {ex.Message}";
        }
        await LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilterToRows();
    partial void OnStatusFilterChanged(CommissionStatus? value) => ApplyFilterToRows();

    private System.Collections.Generic.List<Commission> _allLoaded = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            _allLoaded = FilteredByClient is not null
                ? await _uow.Commissions.GetByClientIdAsync(FilteredByClient.Id)
                : await _uow.Commissions.GetAllAsync();

            ApplyFilterToRows();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load commissions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    private void ApplyFilterToRows()
    {
        var filtered = _allLoaded.Where(c => !c.IsDeleted);

        if (StatusFilter.HasValue)
            filtered = filtered.Where(c => c.Status == StatusFilter.Value);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim().ToLower();
            filtered = filtered.Where(c =>
                c.Title.ToLower().Contains(q) ||
                (c.ServiceType?.ToLower().Contains(q) ?? false));
        }

        Rows.Clear();

        foreach (var c in OrderCommissions(filtered))
        {
            var row = new CommissionRowViewModel(c);
            row.StatusChangeRequested += OnRowStatusChangeRequested;
            Rows.Add(row);
        }
    }

    /// <summary>
    /// DeadlineSoonest puts nulls (no deadline set) last regardless of sort
    /// direction - "soonest" has no meaningful answer for a commission
    /// without one, so it reads better parked at the end than sorted as if
    /// it were the earliest possible date.
    /// </summary>
    private IEnumerable<Commission> OrderCommissions(IEnumerable<Commission> source) => SortOption switch
    {
        ListSortOption.Oldest => source.OrderBy(c => c.DateAdded),
        ListSortOption.AmountHighToLow => source.OrderByDescending(c => c.Price),
        ListSortOption.AmountLowToHigh => source.OrderBy(c => c.Price),
        ListSortOption.DeadlineSoonest => source.OrderBy(c => c.Deadline is null).ThenBy(c => c.Deadline),
        ListSortOption.NameAZ => source.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
        _ => source.OrderByDescending(c => c.DateAdded), // Newest (default)
    };

    private async void OnRowStatusChangeRequested(CommissionRowViewModel row, CommissionStatus previousStatus)
    {
        // async void event handler - any unhandled exception here crashes
        // the whole app (confirmed live: this exact method was the crash
        // site in an earlier "SQLite cannot Sum decimal" report). That
        // underlying bug is fixed, but async void means this method has no
        // safety net for ANY future exception, so it gets one now rather
        // than relying on every possible failure mode underneath it always
        // being bug-free forever.
        try
        {
            // This is the one place besides the full edit form that can trigger
            // the recurrence auto-spawn rule (Phase 1 audit §2.2) - both paths
            // funnel through CommissionService.QuickStatusChangeAsync so the
            // spawn behavior is identical regardless of which UI path was used.
            row.Commission.Status = row.SelectedStatus;
            await _commissionService.QuickStatusChangeAsync(row.Commission, previousStatus);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            // Deliberately not row.SelectedStatus = previousStatus here -
            // SelectedStatus is an [ObservableProperty] whose changed-hook
            // re-raises StatusChangeRequested on every set, so reverting it
            // that way would re-enter this same handler. Resetting the
            // underlying Commission directly and reloading instead rebuilds
            // Rows from scratch (ApplyFilterToRows), which naturally gives
            // this row a fresh CommissionRowViewModel with SelectedStatus
            // correctly re-initialized from the real (unchanged) status.
            row.Commission.Status = previousStatus;
            await LoadAsync(); // clears ErrorMessage on success - set ours after, not before
            ErrorMessage = $"Could not update status: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var formVm = new CommissionFormViewModel(_commissionService, _uow);
        if (FilteredByClient is not null)
            formVm.PreselectClient(FilteredByClient.Id);

        await ShowFormAsync(formVm);
    }

    [RelayCommand]
    private async Task EditAsync(CommissionRowViewModel? row)
    {
        if (row is null) return;
        var formVm = new CommissionFormViewModel(_commissionService, _uow, row.Commission);
        await ShowFormAsync(formVm);
    }

    private async Task ShowFormAsync(CommissionFormViewModel formVm)
    {
        var window = new Views.Commissions.CommissionFormWindow { DataContext = formVm };
        Commission? result = null;
        formVm.Saved += c => { result = c; window.Close(); };
        formVm.Cancelled += window.Close;

        await _dialogService.ShowAsync<object>(window);
        if (result is not null)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task DuplicateAsync(CommissionRowViewModel? row)
    {
        if (row is null) return;
        await _commissionService.DuplicateAsync(row.Commission);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(CommissionRowViewModel? row)
    {
        if (row is null) return;

        // No cascade guard - matches PWA (simple confirm-only delete),
        // Phase 1 audit §2.2.
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Commission?",
            $"\"{row.Commission.Title}\" will be moved to the trash (recoverable for 30 days). Linked payments and receipts are not affected by this action.",
            isDestructive: true,
            confirmLabel: "Delete");

        if (!confirmed) return;

        await _commissionService.DeleteAsync(row.Commission);
        await LoadAsync();
    }

    [RelayCommand]
    private void ViewClient(CommissionRowViewModel? row)
    {
        if (row is null) return;
        _navigation.NavigateTo(AppPage.Clients, row.Commission.ClientId);
    }

    [RelayCommand]
    private void View(CommissionRowViewModel? row)
    {
        if (row is null) return;
        _navigation.NavigateTo(AppPage.CommissionDetail, row.Commission.Id);
    }
}
