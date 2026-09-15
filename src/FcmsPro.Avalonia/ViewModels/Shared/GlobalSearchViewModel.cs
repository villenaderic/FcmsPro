using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FcmsPro.Avalonia.Services;
using FcmsPro.Core.Services;

namespace FcmsPro.Avalonia.ViewModels.Shared;

/// <summary>
/// Powers the "/" global search window (KeySequenceService.FocusSearchRequested).
/// Every existing list page's search box only filters that page's own
/// already-loaded rows; this is the one place that searches Clients,
/// Commissions, Invoices, Quotes, and Expenses at once, from anywhere in the app.
///
/// Debounces on a short delay rather than searching on every keystroke -
/// GlobalSearchService.SearchAsync does five full-table loads per call, and
/// this is a local SQLite file with no query result caching, so debouncing
/// avoids firing that work once per character while someone is still typing.
/// </summary>
public partial class GlobalSearchViewModel : ObservableObject
{
    private readonly GlobalSearchService _searchService;
    private readonly NavigationService _navigation;
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isSearching;

    public ObservableCollection<GlobalSearchResult> Results { get; } = new();

    /// <summary>Bound to the window's Enter-key binding so hitting Enter jumps to the top match without needing to click it.</summary>
    [ObservableProperty] private GlobalSearchResult? _topResult;

    public bool ShowHint => Query.Trim().Length < 2;
    public bool HasResults => Results.Count > 0;
    public bool HasQueryWithNoResults => !IsSearching && !ShowHint && Results.Count == 0;

    public event Action? CloseRequested;

    public GlobalSearchViewModel(GlobalSearchService searchService, NavigationService navigation)
    {
        _searchService = searchService;
        _navigation = navigation;
    }

    partial void OnQueryChanged(string value)
    {
        _debounceCts?.Cancel();
        OnPropertyChanged(nameof(ShowHint));

        if (value.Trim().Length < 2)
        {
            Results.Clear();
            TopResult = null;
            IsSearching = false;
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasQueryWithNoResults));
            return;
        }

        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = DebouncedSearchAsync(value, cts.Token);
    }

    private async Task DebouncedSearchAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(200, ct);
            if (ct.IsCancellationRequested) return;

            IsSearching = true;
            var results = await _searchService.SearchAsync(query, ct);
            if (ct.IsCancellationRequested) return;

            Results.Clear();
            foreach (var r in results)
                Results.Add(r);
            TopResult = Results.FirstOrDefault();
            OnPropertyChanged(nameof(HasResults));
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer keystroke - expected, not an error
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsSearching = false;
                OnPropertyChanged(nameof(HasQueryWithNoResults));
            }
        }
    }

    [RelayCommand]
    private void SelectResult(GlobalSearchResult? result)
    {
        if (result is null) return;

        switch (result.Type)
        {
            case GlobalSearchResultType.Client:
                _navigation.NavigateTo(AppPage.Clients, result.Id);
                break;

            case GlobalSearchResultType.Commission:
                _navigation.NavigateTo(AppPage.CommissionDetail, result.Id);
                break;

            // Invoices/Quotes/Expenses have no dedicated detail page yet -
            // land on the matching list, filtered to the client where one
            // exists (Expenses have no client relationship, so unfiltered).
            case GlobalSearchResultType.Invoice:
                _navigation.NavigateTo(AppPage.Invoices, result.ClientId);
                break;

            case GlobalSearchResultType.Quote:
                _navigation.NavigateTo(AppPage.Quotes, result.ClientId);
                break;

            case GlobalSearchResultType.Expense:
                _navigation.NavigateTo(AppPage.Expenses);
                break;
        }

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
